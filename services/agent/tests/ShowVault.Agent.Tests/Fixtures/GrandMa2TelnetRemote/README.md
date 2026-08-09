# grandMA2 Telnet Remote fixtures

These synthetic, path-free responses model the console and onPC behavior documented in MA Lighting's official [grandMA2 Telnet Remote manual](https://help2.malighting.com/grandMA2/en/help/key_remote_control_telnet.html). They are not captures from production equipment.

The enabled cases contain the documented unauthenticated `guest` and `Please login !` markers. The partial, generic Telnet, and grandMA3 cases deliberately omit that exact combination and must remain non-matches. A disabled service is modeled separately by the test with no listener on TCP 30000.
