## 1. Native host

- [x] 1.1 Detect an output that stops without a request and report `outputLost` with a reason
- [x] 1.2 Use CRF or CQP from the request's quality value; drop the `zerolatency` tune
- [x] 1.3 Write mix, system and microphone audio tracks when both sources are on
- [x] 1.4 Apply source volume at start and through the `volume` op; add rnnoise noise suppression
- [x] 1.5 Report peak levels for system sound and microphone

## 2. Core and client

- [x] 2.1 Add `DiskSpaceRules` with start and while-recording verdicts
- [x] 2.2 Add quality targets, audio volume and noise-suppression settings, request and status fields
- [x] 2.3 Send the new start fields and `SetVolumeAsync`; map every stream in ffmpeg jobs

## 3. App

- [x] 3.1 Refuse or warn on low disk before start; check every ten seconds while recording
- [x] 3.2 Finish the recording and notify when the output is lost
- [x] 3.3 Add volume sliders and the noise-suppression toggle; apply volume live
- [x] 3.4 Show system and microphone level meters on the recording bar
- [x] 3.5 Localize the new text in all five languages

## 4. Verification

- [x] 4.1 Unit tests for disk rules and quality targets
- [x] 4.2 Host test: three audio tracks survive repair; live volume change succeeds
