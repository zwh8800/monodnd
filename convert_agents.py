#!/usr/bin/env python3
"""
将 CCGS 代理格式转换为 opencode 代理格式

CCGS 代理格式：
---
name: creative-director
description: "..."
tools: Read, Glob, Grep, Write, Edit, WebSearch
model: opus
maxTurns: 30
memory: user
disallowedTools: Bash
skills: [brainstorm, design-review]
---
系统提示词

opencode 代理格式：
---
description: "..."
mode: subagent
model: opencode-go/deepseek-v4-pro
steps: 30
permission:
  edit: allow
  bash: deny
---
系统提示词
"""

import os
import re
from pathlib import Path

# 模型映射：CCGS模型 → opencode模型
MODEL_MAPPING = {
    "opus": "opencode-go/glm-5.1",              # 最强模型，用于导演
    "sonnet": "deepseek/deepseek-v4-pro",        # 平衡模型，用于部门主管和专家
    "haiku": "deepseek/deepseek-v4-flash",       # 快速模型，用于轻量任务
}

# 工具到权限的映射
TOOL_PERMISSION_MAPPING = {
    "Read": "read",
    "Glob": "glob",
    "Grep": "grep",
    "Write": "edit",
    "Edit": "edit",
    "Bash": "bash",
    "WebSearch": "websearch",
    "WebFetch": "webfetch",
    "Task": "task",
    "AskUserQuestion": "question",
    "LSP": "lsp",
    "Skill": "skill",
}


def parse_ccgs_agent(file_path):
    """解析 CCGS 代理文件"""
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # 分离 YAML frontmatter 和内容
    parts = content.split('---', 2)
    if len(parts) < 3:
        return None
    
    # 解析 YAML frontmatter
    frontmatter = {}
    lines = parts[1].strip().split('\n')
    for line in lines:
        line = line.strip()
        if not line or line.startswith('#'):
            continue
        
        # 处理 key: value 格式
        if ':' in line:
            key, value = line.split(':', 1)
            key = key.strip()
            value = value.strip()
            
            # 处理引号
            if value.startswith('"') and value.endswith('"'):
                value = value[1:-1]
            elif value.startswith("'") and value.endswith("'"):
                value = value[1:-1]
            
            # 处理列表格式 [item1, item2]
            if value.startswith('[') and value.endswith(']'):
                value = [item.strip() for item in value[1:-1].split(',')]
            
            frontmatter[key] = value
    
    # 系统提示词
    system_prompt = parts[2].strip()
    
    return {
        'frontmatter': frontmatter,
        'system_prompt': system_prompt
    }


def convert_tools_to_permission(tools_str, disallowed_tools_str=None):
    """将工具列表转换为权限格式"""
    permission = {}
    
    # 解析允许的工具
    if tools_str:
        tools = [t.strip() for t in tools_str.split(',')]
        for tool in tools:
            if tool in TOOL_PERMISSION_MAPPING:
                perm_key = TOOL_PERMISSION_MAPPING[tool]
                permission[perm_key] = "allow"
    
    # 解析禁止的工具
    if disallowed_tools_str:
        disallowed = [t.strip() for t in disallowed_tools_str.split(',')]
        for tool in disallowed:
            if tool in TOOL_PERMISSION_MAPPING:
                perm_key = TOOL_PERMISSION_MAPPING[tool]
                permission[perm_key] = "deny"
    
    return permission if permission else None


def convert_agent(file_path, output_dir):
    """转换单个代理文件"""
    agent_data = parse_ccgs_agent(file_path)
    if not agent_data:
        return False
    
    frontmatter = agent_data['frontmatter']
    system_prompt = agent_data['system_prompt']
    
    # 构建 opencode frontmatter
    opencode_frontmatter = {}
    
    # description（必需）
    if 'description' in frontmatter:
        opencode_frontmatter['description'] = frontmatter['description']
    
    # mode（默认 subagent）
    opencode_frontmatter['mode'] = 'subagent'
    
    # model（转换）
    if 'model' in frontmatter:
        ccgs_model = frontmatter['model'].lower()
        if ccgs_model in MODEL_MAPPING:
            opencode_frontmatter['model'] = MODEL_MAPPING[ccgs_model]
        else:
            # 如果不在映射中，保留原样
            opencode_frontmatter['model'] = frontmatter['model']
    
    # steps（从 maxTurns 转换）
    if 'maxTurns' in frontmatter:
        opencode_frontmatter['steps'] = frontmatter['maxTurns']
    
    # permission（从 tools 和 disallowedTools 转换）
    permission = convert_tools_to_permission(
        frontmatter.get('tools'),
        frontmatter.get('disallowedTools')
    )
    if permission:
        opencode_frontmatter['permission'] = permission
    
    # 构建输出内容
    output_content = "---\n"
    for key, value in opencode_frontmatter.items():
        if isinstance(value, dict):
            output_content += f"{key}:\n"
            for sub_key, sub_value in value.items():
                output_content += f"  {sub_key}: {sub_value}\n"
        elif isinstance(value, list):
            output_content += f"{key}: {', '.join(value)}\n"
        else:
            output_content += f"{key}: {value}\n"
    output_content += "---\n\n"
    output_content += system_prompt
    
    # 写入文件
    file_name = Path(file_path).stem
    output_path = os.path.join(output_dir, f"{file_name}.md")
    with open(output_path, 'w', encoding='utf-8') as f:
        f.write(output_content)
    
    return True


def main():
    """主函数"""
    # 源目录
    source_dir = ".claude/agents"
    # 输出目录
    output_dir = ".opencode/agents"
    
    # 创建输出目录
    os.makedirs(output_dir, exist_ok=True)
    
    # 获取所有代理文件
    agent_files = list(Path(source_dir).glob("*.md"))
    
    print(f"找到 {len(agent_files)} 个代理文件")
    
    # 转换每个代理
    success_count = 0
    for agent_file in agent_files:
        if convert_agent(str(agent_file), output_dir):
            success_count += 1
            print(f"✓ 转换成功: {agent_file.name}")
        else:
            print(f"✗ 转换失败: {agent_file.name}")
    
    print(f"\n转换完成: {success_count}/{len(agent_files)} 个代理")
    print(f"输出目录: {output_dir}")


if __name__ == "__main__":
    main()
