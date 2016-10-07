In one senctence: You may re-use the whole project under the three-clause BSD license. Mind the details below.

Authors
====================================================================
An incomplete list of authors, their work and their copyright

None of the authors listed, who are primarily listed for informational purposes,
and as an expression of appreciation, endorsed development of opusenc.js.

* Opusenc
    * Copyright (C)2002-2011 Jean-Marc Valin
    * Copyright (C)2007-2013 Xiph.Org Foundation
    * Copyright (C)2008-2013 Gregory Maxwell

* wav_io.c
    * Copyright (C) 2002 Jean-Marc Valin

* audio-in.c
    * Copyright 2000-2002, Michael Smith <msmith@xiph.org>
    * Copyright 2010, Monty <monty@xiph.org>
    
* lpc.c
    * Copyright 1992, 1993, 1994 by Jutta Degener and Carsten Bormann, Technische Universität Berlin

* resample.c
    * Copyright (C) 2007-2008 Jean-Marc Valin
    * Copyright (C) 2008      Thorvald Natvig

* The Opus Codec
    * Jean-Marc Valin (jmvalin@jmvalin.ca)
    * Koen Vos (koenvos74@gmail.com)
    * Timothy Terriberry (tterribe@xiph.org)
    * Karsten Vandborg Sorensen (karsten.vandborg.sorensen@skype.net)
    * Soren Skak Jensen (ssjensen@gn.com)
    * Gregory Maxwell (greg@xiph.org)

* Libogg
    * Monty <monty@xiph.org>
    * Greg Maxwell <greg@xiph.org>
    * Ralph Giles <giles@xiph.org>
    * Cristian Adam <cristian.adam@gmail.com>
    * Tim Terriberry <tterribe@xiph.org>
    * and the rest of the Xiph.Org Foundation.

* FLAC
    * Copyright (C) 2001-2009  Josh Coalson
    * Copyright (C) 2011-2014  Xiph.Org Foundation
    * "lvqcl" <lvqcl@users.sourceforge.net>

* Emscripten
    * @kripken (Alon Zakai) [and others](https://github.com/kripken/emscripten/blob/master/AUTHORS)

* LLVM
    * [LLVM Developer Group](https://github.com/llvm-mirror/llvm/blob/master/CREDITS.TXT)

Software licenses
====================================================================

This repository includes an Emscripten build of a modified version of `opus-tools/opusenc`. It is modified in a way to allow compilition with LLVM and convenient re-use from the JavaScript world.


Opus
------------------------------------------------------------------
Copyright 2001-2011 Xiph.Org, Skype Limited, Octasic,
                    Jean-Marc Valin, Timothy B. Terriberry,
                    CSIRO, Gregory Maxwell, Mark Borgerding,
                    Erik de Castro Lopo

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions
are met:

- Redistributions of source code must retain the above copyright
notice, this list of conditions and the following disclaimer.

- Redistributions in binary form must reproduce the above copyright
notice, this list of conditions and the following disclaimer in the
documentation and/or other materials provided with the distribution.

- Neither the name of Internet Society, IETF or IETF Trust, nor the
names of specific contributors, may be used to endorse or promote
products derived from this software without specific prior written
permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER
OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

Opus is subject to the royalty-free patent licenses which are
specified at:

Xiph.Org Foundation:
https://datatracker.ietf.org/ipr/1524/

Microsoft Corporation:
https://datatracker.ietf.org/ipr/1914/

Broadcom Corporation:
https://datatracker.ietf.org/ipr/1526/


libOgg
------------------------------------------------------------------
Copyright (c) 2002, Xiph.org Foundation

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions
are met:

- Redistributions of source code must retain the above copyright
notice, this list of conditions and the following disclaimer.

- Redistributions in binary form must reproduce the above copyright
notice, this list of conditions and the following disclaimer in the
documentation and/or other materials provided with the distribution.

- Neither the name of the Xiph.org Foundation nor the names of its
contributors may be used to endorse or promote products derived from
this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED.  IN NO EVENT SHALL THE FOUNDATION
OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.



FLAC
------------------------------------------------------------------
Copyright (C) 2000-2009  Josh Coalson
Copyright (C) 2011-2014  Xiph.Org Foundation

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions
are met:

- Redistributions of source code must retain the above copyright
notice, this list of conditions and the following disclaimer.

- Redistributions in binary form must reproduce the above copyright
notice, this list of conditions and the following disclaimer in the
documentation and/or other materials provided with the distribution.

- Neither the name of the Xiph.org Foundation nor the names of its
contributors may be used to endorse or promote products derived from
this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED.  IN NO EVENT SHALL THE FOUNDATION OR
CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.


opus-tools/opusenc and the opus encoder, including the SILK and CELT implementations used
------------------------------------------------------------------
Opus-tools, with the exception of opusinfo.[ch] is available under
the following two clause BSD-style license:

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions
are met:

- Redistributions of source code must retain the above copyright
notice, this list of conditions and the following disclaimer.

- Redistributions in binary form must reproduce the above copyright
notice, this list of conditions and the following disclaimer in the
documentation and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER
OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

Emscripten
------------------------------------------------------------------
Several parts of the C standard library were implemeted by Emscripten
developers in JavaScript and compiled together with the Opus encoder code.

Emscripten is available under 2 licenses, the MIT license and the
University of Illinois/NCSA Open Source License.

Both are permissive open source licenses, with little if any
practical difference between them.

The reason for offering both is that (1) the MIT license is
well-known, while (2) the University of Illinois/NCSA Open Source
License allows Emscripten's code to be integrated upstream into
LLVM, which uses that license, should the opportunity arise.

The full text of both licenses follows.

 ==============================================================================

Copyright (c) 2010-2014 Emscripten authors, see AUTHORS file.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.

 ==============================================================================

Copyright (c) 2010-2014 Emscripten authors, see AUTHORS file.
All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a
copy of this software and associated documentation files (the
"Software"), to deal with the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to
the following conditions:

    Redistributions of source code must retain the above copyright
    notice, this list of conditions and the following disclaimers.

    Redistributions in binary form must reproduce the above
    copyright notice, this list of conditions and the following disclaimers
    in the documentation and/or other materials provided with the
    distribution.

    Neither the names of Mozilla,
    nor the names of its contributors may be used to endorse
    or promote products derived from this Software without specific prior
    written permission. 

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
IN NO EVENT SHALL THE CONTRIBUTORS OR COPYRIGHT HOLDERS BE LIABLE FOR
ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
SOFTWARE OR THE USE OR OTHER DEALINGS WITH THE SOFTWARE.

Other software licenses
------------------------------------------------------------------
For every other file included in this repository, its license is provided within each file. Everything written by Rillke is available under the [MIT license](http://opensource.org/licenses/MIT) and, at your option under [CC-By-SA 3.0](http://creativecommons.org/licenses/by-sa/3.0/) and, at your option under the three-clause BSD license.

Contributor's license
------------------------------------------------------------------
If you submit a patch to this repository, you agree to license your code under the license the concerning file is licensed under and additionally under the [MIT license](http://opensource.org/licenses/MIT), [CC-By-SA 3.0](http://creativecommons.org/licenses/by-sa/3.0/) and the three-clause BSD license.

Patent licenses
====================================================================

Four companies that did not directly participate in the development of Opus, Qualcomm, Huawei, France Telecom, and Ericsson, filed [IPR disclosures](https://datatracker.ietf.org/ipr/search/?draft=&rfc=6716&submit=rfc) with potentially royalty-bearing terms.

Xiph.Org
--------------------------------------------------------------------
https://datatracker.ietf.org/ipr/1524/

Patents/Applications covered

    US 61/284,154
    US 61/450,041
    US 61/450,053
    US 61/450,060
    and any other applicable


License Grant. Xiph.Org Foundation (“Xiph”) hereby grants to you a perpetual, worldwide, non-exclusive, no-charge, royalty-free, irrevocable (except as stated in this license) license under Licensed Patents to make, have made, use, offer to sell, sell, import, transfer, and otherwise run, modify (in a way that still complies with the Specification), and reproduce any Implementation.

Definitions. Specification means, and includes the following, both individually and collectively, (a) any standard specification of the Opus codec adopted by the IETF Codec Working Group (“Standard”) and (b) any reference implementation (each, a “Reference Implementation”) published by the IETF Codec Working Group in the request for comments (“RFC”) issued by the IETF for the Specification draft for which this License is issued, or any RFC that is issued as an update or new version thereof. An Implementation means any Reference Implementation, or another implementation that complies with the Specification. Licensed Patents means all patents currently owned by Xiph or acquired hereafter that Xiph has the right to license as set forth above and that are necessarily infringed by the Specification, where “necessarily infringed” means: in the case of (a) above, there is no commercially viable means of implementing the Specification without infringing such patent; in the case of (b) above, use of the reference implementation to the extent it infringes such patent.

Termination. If you, directly or indirectly via controlled affiliate or subsidiary, agent, or exclusive licensee, file a Claim for patent infringement against any entity alleging that an Implementation in whole or in part constitutes direct or contributory patent infringement, or inducement of patent infringement (a “Claim”), provided that a Reference Implementation also infringes the patents asserted in the Claim, then any patent rights granted to you under this License shall automatically terminate retroactively as of the date you first received the grant. Claims made against an Implementation in part will only trigger termination if the Implementation in part was done for the purpose of combining it with other technology that complies with the Specification so that the technology’s ultimate use will be consistent with the Standard as a whole.

Broadcom
--------------------------------------------------------------------
https://datatracker.ietf.org/ipr/1526/

Patents/Applications covered

    US 61/406,106
    US 61/394,842
    US 7,353,168
    and any other applicable



License Grant. Broadcom Corporation (“Broadcom”) hereby grants to you a perpetual, worldwide, non-exclusive, no-charge, royalty-free, irrevocable (except as stated in this license) license under Licensed Patents to make, have made, use, offer to sell, sell, import, transfer, and otherwise run, modify (in a way that still complies with the Specification), and reproduce any Implementation.

Definitions. Specification means, and includes the following, both individually and collectively, (a) any standard specification of the Opus codec adopted by the IETF Codec Working Group (“Standard”) and (b) any reference implementation (each, a “Reference Implementation”) published by the IETF Codec Working Group in the request for comments (“RFC”) issued by the IETF for the Specification draft for which this License is issued, or any RFC that is issued as an update or new version thereof. An Implementation means any Reference Implementation, or another implementation that complies with the Specification. Licensed Patents means all patents currently owned by Broadcom or acquired hereafter that Broadcom has the right to license as set forth above and that are necessarily infringed by the Specification, where “necessarily infringed” means: in the case of (a) above, there is no commercially viable means of implementing the Specification without infringing such patent; in the case of (b) above, use of the reference implementation to the extent it infringes such patent.

Termination. If you, directly or indirectly via controlled affiliate or subsidiary, agent, or exclusive licensee, file a Claim for patent infringement against any entity alleging that an Implementation in whole or in part constitutes direct or contributory patent infringement, or inducement of patent infringement (a “Claim”), provided that a Reference Implementation also infringes the patents asserted in the Claim, then any patent rights granted to you under this License shall automatically terminate retroactively as of the date you first received the grant. Claims made against an Implementation in part will only trigger termination if the Implementation in part was done for the purpose of combining it with other technology that complies with the Specification so that the technology’s ultimate use will be consistent with the Standard as a whole.

Microsoft
--------------------------------------------------------------------
https://datatracker.ietf.org/ipr/1914/

Patents/Applications covered

    US-2008-0201137-A1
    US-2010-0174535-A1
    US-2010-0174534-A1
    US-2010-0174547-A1
    US-2010-0174532-A1
    US-2010-0174537-A1
    US-2010-0174542-A1
    US-2010-0174531-A1
    US-2010-0174541-A1
    US-2010-0174538-A1
    US-2011-0077940-A1
    and any other applicable



Microsoft Opus Patent Terms

11-7-2012

1. Patent Terms.

1.1. Specification License. Subject to all the terms and conditions of this Agreement, I, on behalf of myself and my successors in interest and assigns, hereby grant you a non-sublicensable, perpetual, worldwide, non-exclusive, no-charge, royalty-free, irrevocable license to my Necessary Decoder Claims for your Specification Implementation.

1.2. Code License. Subject to all the terms and conditions of this Agreement, I, on behalf of myself and my successors in interest and assigns, hereby grant you a non-sublicensable, perpetual, worldwide, non-exclusive, no-charge, royalty-free, irrevocable patent license to my Necessary Reference Implementation Claims to make, use, sell, offer for sale, import or distribute a Code Implementation.

1.3. Conditions.

1.3.1. Availability. If you own or control Necessary Claims, the licenses set forth in Section 1 are subject to and will become effective starting on the date that you make a binding public irrevocable commitment to license, on reasonable and non-discriminatory royalty-free licensing terms 1) your Necessary Decoder Claims to all implementers for Specification Implementations, and 2) your Necessary Reference Implementation Claims to all implementers for Code Implementations, where the terms of this Agreement satisfy any reciprocity requirements in your reasonable and non-discriminatory royalty-free licensing terms. The promises set forth in Section 1 will remain in effect so long as you continue to make such claims available for Specification Implementations and Code Implementations under reasonable and non-discriminatory royalty-free licensing terms. In addition, as a condition of the licenses set forth in Section 1, you acknowledge and agree that you have not and will not knowingly take any action for the purpose of circumventing the conditions in this Section 1. Notwithstanding the foregoing, you are not required to make the commitments set forth in this Section 1.3.1 as a result of merely using a Specification Implementation or a Code Implementation as an end-user.

1.3.2. Additional Conditions. This license is directly from me to you and you acknowledge as a condition of benefiting from it that no rights from me are received from suppliers, distributors, or otherwise in connection with this license. This license is not an assurance (i) that any of my issued patent claims covers a Specification Implementation or Code Implementation or are enforceable or (ii) that a Specification Implementation or Code Implementation would not infringe intellectual property rights of any third party.

1.4. Termination. All rights, grants, and promises made by me to you under Section 1 are immediately terminated if you or your agent file, maintain, or voluntarily participate in a lawsuit against me or any person or entity asserting that a Specification Implementation infringes Necessary Decoder Claims or a Code Implementation infringes Necessary Reference Implementation Claims, unless that suit was in response to a corresponding suit regarding a Specification Implementation or Code Implementation first brought against you. In addition, all rights, grants, and promises made by me to you under Section 1 are terminated if you, your agent, or successor in interest seek to license Necessary Decoder Claims for Specification Implementations or Necessary Reference Implementations Claims for Code Implementations on a royalty-bearing basis, unless that royalty-bearing licensing activity is in addition to, and not in lieu of, reasonable and non-discriminatory royalty-free licensing terms for Necessary Decoder Claims for Specification Implementations or Necessary Reference Implementation Claims for Code Implementations. This Agreement may also be terminated, including back to the date of non-compliance, because of non-compliance with any other term or condition of this Agreement.

2. Patent License Commitment. On behalf of me and my successors in interest and assigns, I agree to offer alternative reasonable and non-discriminatory royalty-bearing licensing terms 1) to my Necessary Decoder Claims solely for your Specification Implementation and 2) to my Necessary Reference Implementations Claims solely for your Code Implementation.

3. Past Skype Declarations. You may, at your option, continue to rely on the terms set forth in Skype’s past declarations made to the IETF for the Opus Audio Codec, subject to the terms of those declarations and in lieu of the terms of this Agreement solely for the patents set forth in those declarations.

4. Good Faith Obligations. I agree that I have not and will not knowingly take any action for the purpose of circumventing my obligations under this Agreement. In addition, I will not 1) seek an injunction or exclusion order against a) Code Implementations for Necessary Reference Implementation Claims or b) Specification Implementations for Necessary Decoder Claims or 2) require that an implementer license its patents back to me, except for Necessary Reference Implementation Claims for Code Implementations and Necessary Decoder Claims for Specification Implementations. I will not transfer Necessary Reference Implementation Claims or Necessary Decoder Claims unless the transferee is subject to these obligations.

5. Disclaimers. I expressly disclaim any warranties (express, implied, or otherwise), including implied warranties of merchantability, non-infringement, fitness for a particular purpose, or title, related to the Specification or Reference Implementation. The entire risk as to implementing or otherwise using the Specification, Specification Implementation, or Code Implementation is assumed by the implementer and user. IN NO EVENT WILL ANY PARTY BE LIABLE TO ANY OTHER PARTY FOR LOST PROFITS OR ANY FORM OF INDIRECT, SPECIAL, INCIDENTAL, OR CONSEQUENTIAL DAMAGES OF ANY CHARACTER FROM ANY CAUSES OF ACTION OF ANY KIND WITH RESPECT TO THIS AGREEMENT, WHETHER BASED ON BREACH OF CONTRACT, TORT (INCLUDING NEGLIGENCE), OR OTHERWISE, AND WHETHER OR NOT THE OTHER PARTY HAS BEEN ADVISED OF THE POSSIBILITY OF SUCH DAMAGE. Nothing in this Agreement requires me to undertake a patent search.

6. Definitions.

6.1. Agreement. “Agreement” means this document, which sets forth the rights, grants, limitations, conditions, obligations, and disclaimers made available for the particular Specification.

6.2. Code Implementation. “Code Implementation” means making, using, selling, offering for sale, importing or distributing 1) the Reference Implementation, or 2) an implementation that, in the case of an encoder, produces a bitstream that can be decoded by a Specification Implementation solely to the extent it produces such a bitstream, and, in the case of decoder, is a Specification Implementation, where that Specification Implementation may also infringe Necessary Reference Implementation Claims.

6.3. Control. “Control” means direct or indirect control of more than 50% of the voting power to elect directors of that corporation, or for any other entity, the power to direct management of such entity.

6.4. I, Me, or My. “I,” “me,” or “my” refers to the party making this declaration, and any entity that I Control.

6.5. Necessary Claims. “Necessary Claims” means Necessary Decoder Claims and Necessary Reference Implementation Claims.

6.6. Necessary Decoder Claims. “Necessary Decoder Claims” are those patent claims that a party owns or controls, including those claims acquired after the date of this declaration, that are necessarily infringed by an implementation of the required portions (including the required elements of optional portions) of the decoder Specification that are described in detail and not merely referenced in the Specification.

6.7. Necessary Reference Implementation Claims. “Necessary Reference Implementation Claims” are those patent claims that a party owns or controls, including those claims acquired after the date of this declaration, that are necessarily infringed by the Reference Implementation. Necessary Reference Implementation Claims do not include claims that would be infringed only as a consequence of further modification of the Reference Implementation.

6.8. Reference Implementation. “Reference Implementation” means the implementation of the Opus encoder and/or decoder code extracted from Appendix A of the Specification.

6.9. Specification. “Specification” means IETF RFC 6716 dated September 2012.

6.10. Specification Implementation. “Specification Implementation” means making, using, selling, offering for sale, importing or distributing any conformant implementation of the decoder set forth in the Specification 1) only to the extent it implements the Specification and 2) so long as all required portions of the Specification are implemented. Specification Implementation also includes any implementation of a decoder included in subsequent versions of RFC 6716 1) only to the extent that it implements the decoder Specification, and 2) so long as all required portions of the decoder Specification are implemented.

6.11. You or Your. “You,” “you,” or “your” means any person or entity who exercises patent rights granted under this Agreement, and any person or entity you Control.
