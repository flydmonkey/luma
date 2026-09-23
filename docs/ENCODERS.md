# 硬件编码器

硬件开关打开时，宿主只尝试 OBS 已注册、且不是 deprecated/internal 的 H.264 编码器，顺序为 `h264_texture_amf`、`jim_nvenc`、`ffmpeg_nvenc`、`obs_qsv11_v2`。废弃的 `obs_qsv11` 不会被当成可用编码器。

启动成功后，session 的 `encoderName` 是实际用上的编码器 id，`usedHardware` 只在该 id 不是 `obs_x264` 时为 true。没有可用硬编，或硬编启动失败时，回退 `obs_x264`，warning 写明原因。日志在初始化时列出每个候选是 available 还是 unavailable。
