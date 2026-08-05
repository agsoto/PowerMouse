# Mouse Hints detection research

## Decision

Use a staged detector for the foreground window:

1. Query the Windows UI Automation (UIA) control view under the foreground HWND.
2. Keep enabled, on-screen elements that expose an interaction pattern or are a conventional interactive control type.
3. If fewer than the configured number of semantic targets are found, capture the target window once and run an available OCR adapter.
4. Merge OCR text regions with UIA targets, preferring UIA whenever regions overlap.
5. Treat every OCR-only result as an approximate click point. Text recognition does not establish that a region is interactive.

This ordering gives the best general coverage-to-cost ratio. UIA provides semantics, bounds, names, and supported actions without reading pixels. Microsoft supplies providers for standard Win32, Windows Forms, and WPF controls, and UIA also proxies controls exposed through Microsoft Active Accessibility (MSAA). Applications with incomplete accessibility providers, custom GPU canvases, games, remote surfaces, and some embedded web content still require a visual fallback.

## Why UIA is the primary source

- UIA is the Windows accessibility and automation abstraction across UI frameworks. Standard controls are exposed automatically; custom controls are visible when their framework or application implements a provider.
- The control view removes much of the raw implementation tree while retaining user-facing controls.
- Interaction patterns are a better actionable-element signal than control type alone. The scaffold recognizes Invoke, Toggle, SelectionItem, ExpandCollapse, Value, and RangeValue availability.
- A single `FindAll` scoped below the application window avoids a desktop-wide descendant traversal. Microsoft warns that desktop descendant searches can visit hundreds or thousands of elements and that manual tree walking is resource-intensive.
- A `CacheRequest` retrieves the small property set in bulk. This reduces cross-process calls and retains only cached data rather than live element references.

References:

- [UI Automation providers overview](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-providersoverview)
- [Obtaining UI Automation elements](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-obtainingelements)
- [Use caching in UI Automation](https://learn.microsoft.com/en-us/dotnet/framework/ui-automation/use-caching-in-ui-automation)
- [UI Automation architecture and MSAA interoperability](https://learn.microsoft.com/en-us/windows/win32/winauto/architecture-and-interoperability)

## OCR choices

OCR is intentionally represented by `IOcrTextRecognizer`; selection belongs in the Windows host because availability differs by OS and hardware.

Preferred order:

1. Windows AI Text Recognition when `GetReadyState` reports `Ready`. It supplies polygon bounds and confidence and Microsoft describes it as faster and more accurate than the legacy engine. It is not a universal baseline because the text-recognition path requires supported AI hardware and an installed model.
2. `Windows.Media.Ocr.OcrEngine` as the broad Windows 10 fallback. It has existed since Windows 10 build 10240 and returns word bounds, but recognition is limited to OCR language packs installed on the device.
3. A bundled local OCR engine only if product testing shows that missing Windows language packs are common enough to justify the model size, startup cost, servicing, and architecture-specific native binaries.

The recognizer should process one snapshot per hint activation, not a continuous frame stream. Crop to the foreground window, cap the longest image dimension, preserve the screen-coordinate transform, and perform OCR off the UI and hook threads. Consider selective 2x scaling only for small text; always scaling a 4K frame wastes CPU and memory bandwidth.

For capture, `Windows.Graphics.Capture` is the preferred modern path where supported because it captures a window or display through the compositor and delivers GPU surfaces. The adapter must handle unsupported/protected content, minimized windows, resize, DPI, HDR-to-SDR conversion, and capture failure. A GDI `PrintWindow` or `BitBlt` implementation can be a compatibility fallback, but neither reliably captures every hardware-accelerated or protected surface.

References:

- [Windows AI Text Recognition](https://learn.microsoft.com/en-us/windows/ai/apis/text-recognition)
- [Windows AI runtime availability](https://learn.microsoft.com/en-us/windows/ai/apis/get-started)
- [Windows.Media.Ocr.OcrEngine](https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr.ocrengine)
- [Windows Graphics Capture](https://learn.microsoft.com/en-us/windows/apps/develop/media-authoring-processing/screen-capture)

## Reliability boundaries

- UIA providers are cross-process code and a defective provider can block a COM call. `ClickHintDetector` bounds the caller's wait, but it cannot terminate the underlying in-process call. Production hardening should run UIA discovery in a reusable broker process that can be recycled after a deadline.
- An unelevated process cannot inspect or operate elevated UI. `uiAccess` has signing, secure-install-location, and accessibility-use requirements and still does not bypass every integrity boundary. The application should fail closed for inaccessible windows rather than request elevation by default.
- OCR finds visible text, not icon-only buttons, gestures, canvas hit regions, or invisible overlays. A later visual provider may add conservative icon/shape candidates, but there is no framework-independent pixel test that proves clickability.
- UIA bounds are physical screen coordinates for a per-monitor-DPI-aware client. Capture adapters must normalize their image-relative bounds into the same coordinate space before merging.
- Virtualized lists expose only realized items. Off-screen elements are deliberately excluded because they cannot receive a direct point click without scrolling.

Reference: [Security considerations for assistive technologies](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-securityoverview).

## Performance targets

The defaults encode initial consumer-PC budgets, not guaranteed completion times:

| Stage | Budget | Notes |
|---|---:|---|
| UIA | 350 ms | One foreground-window control-view query with cached properties |
| OCR | 900 ms | Only when UIA yields fewer than four targets |
| Result set | 500 targets | Prevents excessive overlay and key-assignment work |

Measure p50, p95, maximum working set, and provider timeout rate on Win32, WinUI, WPF, Chromium/Electron, Java, Office, Remote Desktop, a GPU-rendered game menu, mixed-DPI monitors, and 1080p/1440p/4K displays before changing these defaults.

## Scaffold map

- `PowerMouse.ClickHints`: detection contracts, staged orchestration, result merging, UIA classification, OCR result adaptation, and hint-code assignment.
- `PowerMouse.ClickHints.Windows`: foreground-window targeting, the cached Windows UIA provider, and a legacy Windows OCR recognizer over an injected image-capture source.
- `IWindowImageCapture`: boundary for a Windows Graphics Capture implementation and compatibility fallbacks.
- `IOcrTextRecognizer`: boundary for Windows AI, legacy Windows OCR, or another local recognizer.
- `IElementDetectionProvider`: boundary for additional semantic or visual strategies without changing orchestration.
