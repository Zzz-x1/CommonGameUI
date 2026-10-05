# Next steps

## From the first window to game UI

| Need | Read |
| --- | --- |
| Preserve filters and scroll when hiding inventory | [Panel instances](../manual/panels.md) |
| Restore inventory after closing details | [Page history](../manual/stacks.md) |
| Author all settings categories in one resource | [Embedded tabs](../manual/tabs.md) |
| Wait for confirmation before purchasing | [Result dialogs](../manual/modals-and-dialogs.md) |
| Refresh on return and pause decoration on deactivation | [Lifecycle](../manual/lifecycle.md) |
| Animate one page differently depending on its source | [Transitions](../manual/transitions.md) |
| Use native timelines and continuous image transitions | [Timelines](../manual/timeline-animation.md) |
| Prepare inventory before entering the lobby | [Resources](../manual/resources.md) |

Window nodes retain UI state. Keep project services and player data in a separate model, then pass them to nodes through registration factories. See the result-dialog chapter for a confirmation flow.

See [Samples](../samples/index.md) for a full client and [Integrations](../integrations/custom-engine.md) for another engine.
