# Concentus: Opus for Everyone

This project is an effort to port the Opus reference library to work natively in other languages, and to gather together any such ports that may exist. With this code, developers should be left with no excuse to use an inferior codec, regardless of their language or runtime environment.

## Project Status

As of 13 June 2016 this repo contains a _mostly_ functional Opus implementation in portable C#. It it based on libopus master 1.1.2 configured with FIXED_POINT and DISABLE_FLOAT_API, and aims to be bit-exact with its equivalent C library. The decoder path is casually tested and appears to work fine. The encoder path is likewise functional in most cases, but there are lingering bugs causing discrepancies in the CELT codepath, meaning distortions and pops are possible at high bitrates.

Performance-wise, the current build run about 25% as fast as its equivalent libopus build, but there is still huge room for optimization.

In the future I plan to make a Java version as well, but I will wait until the current C# base is relatively optimized and can carry those improvements to another language. Other ports stemming from the C# base are welcome from any contributors.
