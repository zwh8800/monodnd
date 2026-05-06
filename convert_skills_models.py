#!/usr/bin/env python3
"""
替换 .claude/skills 目录下技能文件中的 model 字段

模型映射：
- opus → opencode-go/glm-5.1
- sonnet → deepseek/deepseek-v4-pro
- haiku → deepseek/deepseek-v4-flash
"""

import os
import re
from pathlib import Path

# 模型映射
MODEL_MAPPING = {
    "opus": "opencode-go/glm-5.1",
    "sonnet": "deepseek/deepseek-v4-pro",
    "haiku": "deepseek/deepseek-v4-flash",
}


def replace_model_in_file(file_path):
    """替换文件中的 model 字段"""
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # 检查是否包含 model 字段
    if 'model:' not in content:
        return False
    
    # 替换 model 字段
    for old_model, new_model in MODEL_MAPPING.items():
        # 匹配 "model: opus" 或 "model: sonnet" 或 "model: haiku"
        pattern = f"model: {old_model}"
        if pattern in content:
            content = content.replace(pattern, f"model: {new_model}")
            with open(file_path, 'w', encoding='utf-8') as f:
                f.write(content)
            return True
    
    return False


def main():
    """主函数"""
    skills_dir = Path(".claude/skills")
    
    # 获取所有 SKILL.md 文件
    skill_files = list(skills_dir.glob("*/SKILL.md"))
    
    print(f"找到 {len(skill_files)} 个技能文件")
    
    # 替换每个文件
    success_count = 0
    for skill_file in skill_files:
        if replace_model_in_file(str(skill_file)):
            success_count += 1
            print(f"✓ 替换成功: {skill_file.parent.name}")
        else:
            print(f"- 无需替换: {skill_file.parent.name}")
    
    print(f"\n替换完成: {success_count} 个技能文件")


if __name__ == "__main__":
    main()
