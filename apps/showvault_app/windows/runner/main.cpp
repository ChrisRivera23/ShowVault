#include <flutter/dart_project.h>
#include <flutter/flutter_view_controller.h>
#include <sddl.h>
#include <shellapi.h>
#include <windows.h>

#include <string>
#include <thread>
#include <vector>

#include "auth_callback_protocol.h"
#include "flutter_window.h"
#include "plugin_startup_url_lock.h"
#include "utils.h"

namespace {

struct CallbackChannel {
  std::wstring mutex_name;
  std::wstring pipe_name;
  std::wstring user_sid;
};

bool GetCurrentUserSid(std::wstring* user_sid) {
  HANDLE token = nullptr;
  if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &token)) {
    return false;
  }

  DWORD required_size = 0;
  GetTokenInformation(token, TokenUser, nullptr, 0, &required_size);
  if (GetLastError() != ERROR_INSUFFICIENT_BUFFER) {
    CloseHandle(token);
    return false;
  }

  std::vector<BYTE> buffer(required_size);
  auto* token_user = reinterpret_cast<PTOKEN_USER>(buffer.data());
  if (!GetTokenInformation(token, TokenUser, token_user, required_size,
                           &required_size)) {
    CloseHandle(token);
    return false;
  }
  CloseHandle(token);

  LPWSTR sid_text = nullptr;
  if (!ConvertSidToStringSidW(token_user->User.Sid, &sid_text)) {
    return false;
  }
  *user_sid = sid_text;
  LocalFree(sid_text);
  return true;
}

bool BuildCallbackChannel(CallbackChannel* channel) {
  if (!GetCurrentUserSid(&channel->user_sid)) {
    return false;
  }

  DWORD session_id = 0;
  if (!ProcessIdToSessionId(GetCurrentProcessId(), &session_id)) {
    return false;
  }

  const std::wstring suffix = channel->user_sid + L"." +
                              std::to_wstring(session_id);
  channel->mutex_name = L"Local\\ShowVault.AuthCallback." + suffix;
  channel->pipe_name = L"\\\\.\\pipe\\ShowVault.AuthCallback." + suffix;
  return true;
}

PSECURITY_DESCRIPTOR BuildCurrentUserSecurityDescriptor(
    const std::wstring& user_sid) {
  const std::wstring sddl = L"D:P(A;;GA;;;" + user_sid + L")";
  PSECURITY_DESCRIPTOR descriptor = nullptr;
  if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(
          sddl.c_str(), SDDL_REVISION_1, &descriptor, nullptr)) {
    return nullptr;
  }
  return descriptor;
}

SECURITY_ATTRIBUTES SecurityAttributesFor(PSECURITY_DESCRIPTOR descriptor) {
  SECURITY_ATTRIBUTES attributes = {};
  attributes.nLength = sizeof(attributes);
  attributes.lpSecurityDescriptor = descriptor;
  attributes.bInheritHandle = FALSE;
  return attributes;
}

std::wstring ReadStartupUri() {
  int argument_count = 0;
  LPWSTR* arguments = CommandLineToArgvW(GetCommandLineW(), &argument_count);
  std::wstring uri;
  if (arguments != nullptr && argument_count > 1) {
    uri = arguments[1];
  }
  if (arguments != nullptr) {
    LocalFree(arguments);
  }
  return uri;
}

void SetPluginStartupUrl(const std::wstring& uri) {
  auth0_flutter::WriteLockGuard lock(auth0_flutter::GetPluginUrlRwLock());
  if (lock.IsValid()) {
    SetEnvironmentVariableW(L"PLUGIN_STARTUP_URL", uri.c_str());
  }
}

void BringExistingWindowToFront() {
  HWND window = FindWindowW(L"FLUTTER_RUNNER_WIN32_WINDOW", L"ShowVault");
  if (window != nullptr) {
    ShowWindow(window, SW_RESTORE);
    SetForegroundWindow(window);
  }
}

void ForwardToFirstInstance(const CallbackChannel& channel,
                            const std::wstring& uri) {
  if (!showvault::IsAllowedAuthCallback(uri) ||
      !WaitNamedPipeW(channel.pipe_name.c_str(), 2000)) {
    return;
  }

  HANDLE pipe = CreateFileW(channel.pipe_name.c_str(), GENERIC_WRITE, 0,
                            nullptr, OPEN_EXISTING, 0, nullptr);
  if (pipe == INVALID_HANDLE_VALUE && GetLastError() == ERROR_PIPE_BUSY &&
      WaitNamedPipeW(channel.pipe_name.c_str(), 2000)) {
    pipe = CreateFileW(channel.pipe_name.c_str(), GENERIC_WRITE, 0, nullptr,
                       OPEN_EXISTING, 0, nullptr);
  }
  if (pipe == INVALID_HANDLE_VALUE) {
    return;
  }

  const DWORD byte_count =
      static_cast<DWORD>((uri.size() + 1) * sizeof(wchar_t));
  DWORD bytes_written = 0;
  WriteFile(pipe, uri.c_str(), byte_count, &bytes_written, nullptr);
  CloseHandle(pipe);
}

void StartPipeServer(const CallbackChannel& channel) {
  std::thread([channel] {
    while (true) {
      PSECURITY_DESCRIPTOR descriptor =
          BuildCurrentUserSecurityDescriptor(channel.user_sid);
      if (descriptor == nullptr) {
        return;
      }
      SECURITY_ATTRIBUTES attributes = SecurityAttributesFor(descriptor);

      HANDLE pipe = CreateNamedPipeW(
          channel.pipe_name.c_str(),
          PIPE_ACCESS_INBOUND | FILE_FLAG_FIRST_PIPE_INSTANCE,
          PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT |
              PIPE_REJECT_REMOTE_CLIENTS,
          1, 0, 0, 0, &attributes);
      LocalFree(descriptor);
      if (pipe == INVALID_HANDLE_VALUE) {
        return;
      }

      const BOOL connected = ConnectNamedPipe(pipe, nullptr);
      if (connected || GetLastError() == ERROR_PIPE_CONNECTED) {
        wchar_t buffer[showvault::kMaxAuthCallbackCharacters + 1] = {};
        DWORD bytes_read = 0;
        const BOOL read_ok = ReadFile(pipe, buffer, sizeof(buffer), &bytes_read,
                                      nullptr);
        if (read_ok && bytes_read >= sizeof(wchar_t) &&
            bytes_read % sizeof(wchar_t) == 0) {
          size_t character_count = bytes_read / sizeof(wchar_t);
          if (buffer[character_count - 1] == L'\0') {
            character_count -= 1;
          }
          const std::wstring uri(buffer, character_count);
          if (uri.find(L'\0') == std::wstring::npos &&
              showvault::IsAllowedAuthCallback(uri)) {
            SetPluginStartupUrl(uri);
            BringExistingWindowToFront();
          }
        }
      }

      DisconnectNamedPipe(pipe);
      CloseHandle(pipe);
    }
  }).detach();
}

}  // namespace

int APIENTRY wWinMain(_In_ HINSTANCE instance, _In_opt_ HINSTANCE prev,
                      _In_ wchar_t *command_line, _In_ int show_command) {
  // Attach to console when present (e.g., 'flutter run') or create a
  // new console when running with a debugger.
  if (!::AttachConsole(ATTACH_PARENT_PROCESS) && ::IsDebuggerPresent()) {
    CreateAndAttachConsole();
  }

  // Initialize COM, so that it is available for use in the library and/or
  // plugins.
  ::CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);

  CallbackChannel channel;
  PSECURITY_DESCRIPTOR mutex_descriptor = nullptr;
  HANDLE mutex = nullptr;
  if (!BuildCallbackChannel(&channel)) {
    ::CoUninitialize();
    return EXIT_FAILURE;
  }
  mutex_descriptor = BuildCurrentUserSecurityDescriptor(channel.user_sid);
  if (mutex_descriptor == nullptr) {
    ::CoUninitialize();
    return EXIT_FAILURE;
  }
  SECURITY_ATTRIBUTES mutex_attributes =
      SecurityAttributesFor(mutex_descriptor);
  mutex = CreateMutexW(&mutex_attributes, TRUE, channel.mutex_name.c_str());
  const DWORD mutex_error = GetLastError();
  LocalFree(mutex_descriptor);
  if (mutex == nullptr) {
    ::CoUninitialize();
    return EXIT_FAILURE;
  }

  const std::wstring startup_uri = ReadStartupUri();
  if (mutex_error == ERROR_ALREADY_EXISTS) {
    if (showvault::IsAllowedAuthCallback(startup_uri)) {
      ForwardToFirstInstance(channel, startup_uri);
    }
    BringExistingWindowToFront();
    CloseHandle(mutex);
    ::CoUninitialize();
    return EXIT_SUCCESS;
  }

  SetPluginStartupUrl(showvault::IsAllowedAuthCallback(startup_uri)
                          ? startup_uri
                          : std::wstring());
  StartPipeServer(channel);

  flutter::DartProject project(L"data");

  std::vector<std::string> command_line_arguments =
      GetCommandLineArguments();

  project.set_dart_entrypoint_arguments(std::move(command_line_arguments));

  FlutterWindow window(project);
  Win32Window::Point origin(10, 10);
  Win32Window::Size size(1280, 720);
  if (!window.Create(L"ShowVault", origin, size)) {
    CloseHandle(mutex);
    ::CoUninitialize();
    return EXIT_FAILURE;
  }
  window.SetQuitOnClose(true);

  ::MSG msg;
  while (::GetMessage(&msg, nullptr, 0, 0)) {
    ::TranslateMessage(&msg);
    ::DispatchMessage(&msg);
  }

  CloseHandle(mutex);
  ::CoUninitialize();
  return EXIT_SUCCESS;
}
