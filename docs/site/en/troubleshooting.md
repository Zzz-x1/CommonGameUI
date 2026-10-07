# Troubleshooting

| Problem | Check or fix |
| --- | --- |
| Invisible after attach | Attach only initializes. Show with Open/Push/Switch and check mount size. |
| Buttons blocked | Godot empty mounts use MouseFilter.Ignore; Unity mounts have no raycastable Graphic. |
| Lifecycle reentry | Do not request the active container or its ancestors from preparation/notifications. Child initialization may be awaited. Ordinary repeated clicks queue. |
| Animation flashes | Apply the start before yielding; hide incoming presentation during layout and avoid layout overwrites. |
| Generated calls missing | Reference as Analyzer. Import UIFramework.Generated for registration and the node namespace for calls. |
| Arguments rejected on Single reopen | Arguments are for creation. Reopen without them; refresh through a business method. |
| Cancellation delayed | Non-cancelable native loads must complete and clean up. Keep observing the task. |
| Dialog did not return false | Cancel calls Complete(false). Unanswered external closure cancels the task. |

See [Godot](integrations/godot.md)/[Unity](integrations/unity.md) for supported environments and status. Other engines need an [adapter](integrations/custom-engine.md).

[Async and errors](manual/async-and-errors.md) · [API](api/index.md)
