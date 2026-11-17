# Triage Labels

将五个 canonical triage roles 映射为本项目使用的状态字符串。
本地 Markdown tracker 将这些值写入 issue 的 `Status:` 行。

| Canonical role | Tracker value | 含义 |
| --- | --- | --- |
| needs-triage | needs-triage | 等待维护者评估 |
| needs-info | needs-info | 等待报告者补充信息 |
| ready-for-agent | ready-for-agent | 需求明确，可交给 agent 实现 |
| ready-for-human | ready-for-human | 需要人工实现 |
| wontfix | wontfix | 不予处理 |

当 skill 提到某个 triage role 时，使用对应的 Tracker value。
