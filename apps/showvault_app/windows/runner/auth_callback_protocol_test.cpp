#include "auth_callback_protocol.h"

#include <string>

int main() {
  using showvault::IsAllowedAuthCallback;

  if (!IsAllowedAuthCallback(L"showvault://callback") ||
      !IsAllowedAuthCallback(L"showvault://callback?code=abc&state=123") ||
      !IsAllowedAuthCallback(L"showvault://callback#error=cancelled")) {
    return 1;
  }

  std::wstring embedded_null = L"showvault://callback?code=abc";
  embedded_null.insert(embedded_null.begin() + 20, L'\0');
  const std::wstring overlong =
      std::wstring(showvault::kAuthCallbackBase) + L"?" +
      std::wstring(showvault::kMaxAuthCallbackCharacters, L'a');

  if (IsAllowedAuthCallback(L"") ||
      IsAllowedAuthCallback(L"SHOWVAULT://callback?code=abc") ||
      IsAllowedAuthCallback(L"showvault://callback.evil?code=abc") ||
      IsAllowedAuthCallback(L"showvault://callback/extra?code=abc") ||
      IsAllowedAuthCallback(L"https://example.invalid/callback") ||
      IsAllowedAuthCallback(embedded_null) || IsAllowedAuthCallback(overlong)) {
    return 1;
  }

  return 0;
}
