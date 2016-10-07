#!/bin/sh
cd libogg
echo -----------------------------------
echo Making libogg
echo -----------------------------------
./autogen.sh
emconfigure ./configure
emmake make
export OGGDIR=$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )/
cd ..

cd flac
echo -----------------------------------
echo Making flac
echo -----------------------------------
./autogen.sh
emconfigure ./configure --with-ogg=$OGGDIR
cd ../libogg
ln -s src/.libs lib
ln -s include/ogg ogg
cd ../flac
emmake make
export FLACDIR=$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )/
cd ..

cd opus
echo -----------------------------------
echo Making opus
echo -----------------------------------
./autogen.sh
emconfigure ./configure CFLAGS="-O3" --disable-extra-programs
emmake make
export OPUSDIR=$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )/
cd ..

cd opus-tools
echo -----------------------------------
echo Building Opus-tools
echo -----------------------------------
rm opusenc.so

./autogen.sh
export OGG_CFLAGS="-I"$OGGDIR"ogg/"
export OGG_LIBS="-L"$OGGDIR"lib/"
export OPUS_CFLAGS="-I"$OPUSDIR"include/"
export OPUS_LIBS="-L"$OPUSDIR".libs/"
export FLAC_CFLAGS="-I"$FLACDIR"include/FLAC/"
export FLAC_LIBS="-I"$FLACDIR"src/libFLAC/.libs/"
emconfigure ./configure
ln -s $OGGDIR"include/ogg" ogg
ln -s $FLACDIR"include/FLAC/" FLAC
emmake make

mv opusenc opusenc.so
echo -----------------------------------
echo Building JavaScript
echo -----------------------------------
em++ -O3 $OGGDIR"lib/libogg.so" $FLACDIR"src/libFLAC/.libs/libFLAC.so" $OPUSDIR".libs/libopus.so" opusenc.so -o opusenc.html -s EXPORTED_FUNCTIONS="['_opus_decoder_create', '_opus_decode_float', '_opus_decoder_destroy', '_encode_buffer']" -s RESERVED_FUNCTION_POINTERS=1
cp -f opusenc.js ../worker/opusenc.js
cp -f opusenc.html.mem ../worker/opusenc.data.js
cd ..
echo -----------------------------------
echo Running Press Ctrl+C to abort
echo -----------------------------------
emrun iframe.html
