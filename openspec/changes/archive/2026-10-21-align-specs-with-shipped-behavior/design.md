## Context

这三处行为都已经随提交 `361e258` 和 `7ffbc5c` 发布，代码、桌面设置页和网页设置页都已一致，只有主规格还停在旧描述。

## Goals / Non-Goals

**Goals:** 让 `screen-recording` 和 `desktop-app` 主规格描述当前行为。

**Non-Goals:** 不恢复登录开录或定时录制，不改停录流程，不改启动时的模式选择。

## Decisions

- 停录和自动化两条要求要删掉原有场景，MODIFIED 不允许丢场景，所以用 REMOVED 加新名字的 ADDED 取代；Four capture modes 和 Persistent settings 保留全部原场景，用 MODIFIED。
- 停录后是否进预览取决于主窗口是否可见，这与 `MainWindow.StopAsync` 里静默和隐藏窗口的分支一致。

## Risks / Trade-offs

- 以后如果要恢复自动化，需要新的变更重新加回要求。
