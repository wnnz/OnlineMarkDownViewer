# Markdown 语法演示

## 列表

- 无序列表 A
- 无序列表 B
- 无序列表 C

1. 有序列表 1
2. 有序列表 2
3. 有序列表 3

## 任务列表

- [x] 完成登录页
- [x] 完成文档浏览
- [x] 完成 Markdown 渲染
- [ ] 继续增强 Git 稀疏检出

## 引用块

> 这是一段引用内容，用来确认暗色主题下的引用样式是否正常。

## 表格

| 功能 | 状态 | 说明 |
| --- | --- | --- |
| 登录 | 已完成 | 静态登录页 |
| 渲染 | 已完成 | Markdig |
| 高亮 | 已完成 | highlight.js |

## 代码块

```csharp
var config = await apiClient.GetConfigAsync();
Console.WriteLine(config.Sources.Count);
```

```json
{
  "name": "默认文档源",
  "kind": "Local"
}
```

```bash
dotnet build MarkDownViewer.slnx
```

## 脚注

脚注演示文本。[^1]

[^1]: 这里是脚注内容。
