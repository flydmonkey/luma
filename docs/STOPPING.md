# 停止录制

`POST /session/stop` 不再等待 libobs 收尾。宿主把 session 标成 phase 4（对外 `stopping`），立刻返回；OBS stop、强制结束和成片校验在后台进行。这段时间 `GET /session` 和界面轮询仍然可以查到 `stopping`，时钟保持停录前的数值。

## 上限

- `obs_output_stop` 若 15 秒没有返回，另一个线程调用 `obs_output_force_stop`。
- stop 返回后先等 stop signal 或 output 变为 inactive，宽限 45 秒。只有到点才 force，然后再等 15 秒。
- 输出已经停住之后，`last_error` 只记日志。编码帧数为 0 时保留停录前的墙钟，不用帧计数把好文件判死。
- 文件有内容，并且（ffmpeg 可用时）容器时长与记录时长相差不超过 `max(1.5s, 时长×0.5%)`，停录成功。若走过 force，`stopForced=true`，warning 说明成片已通过校验。
- 空文件、无法读取的时长，或和记录时长对不上，才失败。
- 控制面最多再等 120 秒。到点仍是 `stopping` 就保留错误并继续服务，不把进程杀掉。

管道断开或自测收尾同样最多等 120 秒，避免短于上面的 native 预算。
