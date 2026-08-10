class WindowsLocalPathPolicy {
  const WindowsLocalPathPolicy._();

  static final RegExp _driveAbsolute = RegExp(r'^[A-Za-z]:[\\/]');

  static bool isSafeLocalAbsolute(String value) {
    if (value.isEmpty || value.trim() != value || value.contains('\u0000')) {
      return false;
    }
    if (value.startsWith(r'\\') || !_driveAbsolute.hasMatch(value)) {
      return false;
    }
    final remainder = value.substring(3);
    if (remainder.isEmpty) return false;
    final segments = remainder.split(RegExp(r'[\\/]'));
    if (segments.any(
      (segment) =>
          segment.isEmpty ||
          segment == '.' ||
          segment == '..' ||
          segment.endsWith(' ') ||
          segment.endsWith('.') ||
          segment.contains(':'),
    )) {
      return false;
    }
    return true;
  }

  static bool sameCanonicalPath(String left, String right) =>
      _normalize(left) == _normalize(right);

  static String finalSegment(String value) =>
      value.replaceAll('/', r'\').split(r'\').last;

  static bool isWithin(String candidate, String root) {
    final normalizedCandidate = _normalize(candidate);
    final normalizedRoot = _normalize(root);
    return normalizedCandidate == normalizedRoot ||
        normalizedCandidate.startsWith('$normalizedRoot\\');
  }

  static String _normalize(String value) {
    var normalized = value.replaceAll('/', r'\').toLowerCase();
    while (normalized.length > 3 && normalized.endsWith(r'\')) {
      normalized = normalized.substring(0, normalized.length - 1);
    }
    return normalized;
  }
}

class LocalDiagnosticPrivacy {
  const LocalDiagnosticPrivacy._();

  static final RegExp _embeddedWindowsDrive = RegExp(
    r'(^|[^A-Za-z0-9])[A-Za-z]:[\\/]',
  );
  static final RegExp _embeddedUnc = RegExp(r'(^|\s)\\\\[^\\\s]+[\\/]');
  static final RegExp _embeddedUnix = RegExp(r'(^|\s)/[^/\s]');

  static bool containsLocalPath(String value) =>
      value.contains('file://') ||
      _embeddedWindowsDrive.hasMatch(value) ||
      _embeddedUnc.hasMatch(value) ||
      _embeddedUnix.hasMatch(value);
}
