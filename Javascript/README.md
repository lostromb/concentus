# opusenc.js [![Build Status](https://travis-ci.org/Rillke/opusenc.js.svg?branch=master)](https://travis-ci.org/Rillke/opusenc.js)

## JavaScript Opus (audio format) Encoder

[**Project Website**](https://blog.rillke.com/opusenc.js/) • [Minimal demo](https://rawgit.com/Rillke/opusenc.js/master/iframe.html)

Opusenc.js encodes whole files to ogg-opus; this is Opus encapsulated into an Ogg container. It is supposed to do the same as the opusenc tools from the opus-tools collection and as such supports features like Vorbis comment, preserving some metadata, reading AIF, WAV, and FLAC (in its native container).

opusenc.js was built with Emscripten.

## Building
If you just want to use opusenc.js, you don't have to build it. In this case, see [using](#using) instead.

### Prerequisites
- A recent linux build system
- Emscripten 1.25.0 installed and activated

### Build script
```bash
git clone git://github.com/Rillke/opusenc.js.git opusenc.js
cd opusenc.js
git submodule update --init
./make.sh
```

## Using
A pre-compiled script together with some auxiliary scripts making use from JavaScript easier is in the `/worker` directory.
[iframe.html](iframe.html) is a minimal usage example. [Test it live](https://rawgit.com/Rillke/opusenc.js/master/iframe.html). It starts the encoding process posting `command: 'encode'` to the worker:
```JavaScript
var worker = new Worker( 'worker/EmsWorkerProxy.js' );
// Files to be read and posted back
// after encoding completed
var outData = {
	// File name
	'encoded.opus': {
		// MIME type
		'MIME': 'audio/ogg'
	}
};

worker.onmessage = function( e ) {
	// Handle incoming data
};

// Prepare files etc.

// Post all data and the encode command
// to the web worker
worker.postMessage( {
	command: 'encode',
	args: args,
	outData: outData,
	fileData: storedFiles
} );
```

- `command`: `'encode'|'prefetch'` DOMString that either starts encoding or prefetching the 850 KiB worker script. Posting a prefetch command in advance is optional, depends on the user experience you'd like to create and does not require further arguments. If the script is not prefetched, it will be downloaded when `'encode'` is invoked.
- `args`: Array holding the command line arguments (DOMString)
- `outData`: Object literal of information about the files that should be read out of the worker's file system after encoding completed
- `fileData`: Object literal of input file data mapping file names to `Uint8Array`s

A more extensive example is available on the [project's website](https://blog.rillke.com/opusenc.js/).

## Contributing
Submit patches to this GitHub repository or [file issues](https://github.com/Rillke/opusenc.js/issues).

## License
See [LICENSE.md](LICENSE.md)
