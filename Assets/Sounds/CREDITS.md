Deltempo interface sounds
========================

Source pack : Interface Sounds (1.0) by Kenney (https://kenney.nl)
License     : Creative Commons Zero v1.0 Universal (CC0 1.0)
             https://creativecommons.org/publicdomain/zero/1.0/
             Free for personal and commercial use. Attribution is not required,
             but Kenney's work is credited here as a matter of courtesy.

Files in this folder, and the originals they were taken from:

  click.wav     <- click_005.wav        19 ms   soft low tick, the routine interaction cue
  success.wav   <- confirmation_001.wav 295 ms  mid-bright confirm, used when an operation succeeds
  error.wav     <- error_007.wav        205 ms  low and dull, deliberately unlike success
  warning.wav   <- question_004.wav     338 ms  warm mid tone for "finished, but check this"

Selection notes
---------------
The pack ships 116 variations. These four were chosen by measuring every candidate
(duration, peak level, RMS, and spectral brightness) and picking the set that stays
soft and non-fatiguing for a tool that is used for long stretches:

  * click fires on nearly every interaction, so it is the shortest and darkest of
    the four - the lowest-frequency option in the pack, so it never becomes grating.
  * error sits at 132 Hz against success at 804 Hz, so the two are distinguishable
    by ear alone, without looking at the screen.
  * every other "error" and "confirmation" in the pack peaks between 2.4 kHz and
    4.9 kHz, which reads as shrill on laptop speakers; those were rejected.

Peak levels are normalised in SoundService at load time so the four cues sit at a
consistent level regardless of how the source files were mastered.

Full upstream license text: KENNEY-LICENSE.txt
