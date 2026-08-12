#include "auth_callback_protocol.h"

namespace showvault {

bool IsAllowedAuthCallback(const std::wstring& uri) {
  if (uri.empty() || uri.size() > kMaxAuthCallbackCharacters) {
    return false;
  }
  if (uri.find(L'\0') != std::wstring::npos) {
    return false;
  }

  const std::wstring callback_base(kAuthCallbackBase);
  if (uri.compare(0, callback_base.size(), callback_base) != 0) {
    return false;
  }
  if (uri.size() == callback_base.size()) {
    return true;
  }

  const wchar_t delimiter = uri[callback_base.size()];
  return delimiter == L'?' || delimiter == L'#';
}

}  // namespace showvault
