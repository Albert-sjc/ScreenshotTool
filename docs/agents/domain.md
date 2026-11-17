# Domain Docs

本项目采用 single-context 布局。

## Before exploring, read these

- 根目录的 `CONTEXT.md`。
- `docs/adr/` 中与当前工作区域相关的 ADRs。

如果这些文件不存在，静默继续。领域术语或决策实际明确后，
由 `/domain-modeling` 按需创建，无需预先生成空文档。

## File structure

- `CONTEXT.md`：项目领域术语与上下文。
- `docs/adr/`：架构决策记录。

以上路径均相对于项目根目录。

## Use the glossary's vocabulary

在 issue title、refactor proposal、hypothesis、test name 等输出中，
使用 `CONTEXT.md` 定义的领域术语，遵循其中的同义词取舍。

如果需要的概念未出现在 glossary 中，先判断它是否符合项目已有语言；
确有缺口时，为 `/domain-modeling` 记录。

## Flag ADR conflicts

如果建议与现有 ADR 矛盾，明确引用该 ADR，
说明冲突点及重新讨论该决策的理由。
