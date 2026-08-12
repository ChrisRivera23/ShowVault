#ifndef RUNNER_AUTH_CALLBACK_PROTOCOL_H_
#define RUNNER_AUTH_CALLBACK_PROTOCOL_H_

#include <string>

namespace showvault {

inline constexpr wchar_t kAuthCallbackBase[] = L"showvault://callback";
inline constexpr size_t kMaxAuthCallbackCharacters = 2047;

bool IsAllowedAuthCallback(const std::wstring& uri);

}  // namespace showvault

#endif  // RUNNER_AUTH_CALLBACK_PROTOCOL_H_
