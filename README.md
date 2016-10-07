# Concentus: Opus for Everyone

This project is an effort to port the Opus reference library to work natively in other languages, and to gather together any such ports that may exist. With this code, developers should be left with no excuse to use an inferior codec, regardless of their language or runtime environment.

## Project Status

This repo contains completely functional Opus implementations in portable C# and Java. They are based on libopus master 1.1.2 configured with FIXED_POINT and with an extra switch to enable/disable the floating-point analysis functions. Both the encoder and decoder paths have been thoroughly tested to be bit-exact with their equivalent C functions in all common use cases. I have also included a port of the libspeexdsp resampler for general use.

Performance-wise, the current build runs about 40-50% as fast as its equivalent libopus build, mostly due to the lack of stack arrays and vectorized intrinsics in managed languages. I do not believe performance will get much better than this; if you need blazing-fast performance then I encourage you to try the P/Opus or JNI library. The API surface is finalized and existing code should not change, but I may add helper classes in the future.

No other ports beyond C# / Java are planned at this time, but pull requests are welcome from any contributors.
