# Mouse Hints detection

Mouse Hints combines UI Automation (UIA) with OCR for the foreground window. The feature is experimental and can be disabled independently.

## Pipeline

1. Query compact command regions first, then the remaining UIA control tree.
2. Keep enabled, visible controls with supported interaction patterns or recognized interactive roles.
3. Capture the window once and run OCR alongside UIA.
4. Merge results, preferring semantic controls over overlapping OCR text.
5. Clip bounds to the target window, limit the target count, and assign hint codes in spatial order.

UIA properties are cached in bulk, and providers return snapshots rather than live automation elements. A timeout after the command pass retains its results; cancellation discards the session. Partial results are not displayed before detection completes or reused across windows.

`EnableOcrAugmentation` is enabled by default because a populated UIA tree can still omit custom panels. Disabling it limits OCR to cases with fewer than `MinimumSemanticTargets` semantic targets.

## Classification

- Structural tree, header, status-bar, and app-bar containers do not receive hints. Their interactive children do.
- Split buttons keep primary and dropdown actions separate.
- For selectable rows with editable cells, the first writable cell anchors the row hint. Enclosed read-only metadata is omitted; independent actions and standalone read-only fields remain available.
- AppBar control type 50040 may be absent in older managed UIA clients. The classifier also recognizes its Toggle/ExpandCollapse pattern combination without Invoke.

## OCR

The app uses `Windows.Media.Ocr.OcrEngine` with `GdiWindowImageCapture`. Recognition depends on installed OCR languages and successful capture.

OCR word groups split at gaps larger than 0.75 times the smaller text height. This separates menu commands while retaining multiword labels, but tightly spaced commands can merge and widely spaced labels can split.

OCR results are approximate click points. Text may not be interactive, and icon-only controls may be missed. Semantic targets take priority when the result limit is reached.

## Limits

Defaults are defined in `DetectionOptions`:

| Setting | Default |
|---|---|
| UIA timeout | 2500 ms |
| OCR timeout | 900 ms |
| Maximum targets | 500 |
| Minimum semantic targets for sparse-only OCR | 4 |

Timeouts bound the caller's wait; they cannot forcibly stop a blocked external UIA COM call. Virtualized controls, protected windows, popup occlusion, and incomplete accessibility providers can reduce coverage. All providers must convert bounds to physical screen coordinates before merging.

## Inspecting results

Focus the target window, then run:

```powershell
PowerMouse.exe --inspect-hints
```

This shows the detected hints and writes `%LOCALAPPDATA%\PowerMouse\Diagnostics\hints.json`, including window bounds, timings, diagnostics, targets, and codes. Check the recorded window handle when comparing results.

The file contains control names and recognized text. Review it before sharing.

## Interfaces

- `IElementDetectionProvider`: semantic target discovery.
- `IIncrementalElementDetectionProvider`: preserves command-pass results if later work fails.
- `IOcrTextRecognizer`: text recognition.
- `IWindowImageCapture`: capture with screen-coordinate mapping.

## References

- [UI Automation caching](https://learn.microsoft.com/en-us/dotnet/framework/ui-automation/use-caching-in-ui-automation)
- [AppBar control type](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supportappbarcontroltype)
- [Windows OCR](https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr.ocrengine)
