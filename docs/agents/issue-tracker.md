# Issue tracker: Local Markdown

本项目的 issues 和 specs 作为 Markdown 文件存放在 `.scratch/` 中。

## Conventions

- 每个 feature 一个目录：`.scratch/<feature-slug>/`。
- Spec 位于 `.scratch/<feature-slug>/spec.md`。
- Implementation issues 每个 ticket 一个文件：
  `.scratch/<feature-slug>/issues/<NN>-<slug>.md`，从 `01` 开始编号。
  不合并为一个 tickets 文件。
- Triage state 记录在 issue 文件顶部附近的 `Status:` 行；
  role 字符串见 `docs/agents/triage-labels.md`。
- Comments 和 conversation history 追加到文件底部的 `## Comments` 下。

## When a skill says "publish to the issue tracker"

在 `.scratch/<feature-slug>/` 下按上述约定创建文件，必要时创建目录。

## When a skill says "fetch the relevant ticket"

读取引用路径处的文件。用户通常会提供路径或 issue number；
仅有编号时，在相关 feature 的 issues 目录中查找。

## Wayfinding operations

供 `/wayfinder` 使用。map 是一个文件，每个 ticket 对应一个 child 文件。

- Map：`.scratch/<effort>/map.md`，保存 Notes / Decisions-so-far / Fog。
- Child ticket：`.scratch/<effort>/issues/NN-<slug>.md`，从 `01` 编号，
  body 中记录问题。`Type:` 记录 research/prototype/grilling/task；
  此类 ticket 的 `Status:` 使用 claimed/resolved。
- Blocking：顶部附近的 `Blocked by: NN, NN` 行。
  列出的每个 ticket 都 resolved 时，当前 ticket 即为 unblocked。
- Frontier：扫描 open、unblocked 且 unclaimed 的文件，取编号最小者。
- Claim：开始工作前设置 `Status: claimed` 并保存。
- Resolve：把答案追加到 `## Answer` 下，设置 `Status: resolved`，
  再向 map 的 Decisions-so-far 追加摘要与相关文件链接。
