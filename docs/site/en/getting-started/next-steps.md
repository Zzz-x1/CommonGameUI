# Next steps

## From menu to game client

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

Put player data into a separate model and inject services through registration factories. Buttons call model operations and refresh after success. Save rewards once; completion or skipping animation must not commit them again.

The project shell owns Esc, shutdown, cross-layer return and controls outside container mounts. Implement concrete game rules before introducing global navigation queues or arbitrary layer presets.

See [Samples](../samples/index.md) for a full client and [Integrations](../integrations/custom-engine.md) for another engine.
