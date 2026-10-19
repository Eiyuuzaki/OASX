# -*- coding: utf-8 -*-
"""_RoleSelector 三行布局适配：hint 改用 shortLabel（含服务器名），加宽 hint。"""
import io
from pathlib import Path

F = Path(r"D:\OAS-Kirin\OASX\lib\views\resource\resource_view.dart")
text = io.open(F, encoding="utf-8").read()

# hint 用 shortLabel（名字+等级+服务器名），行1 独占整行可以放宽到 240
old = """        DropdownButton<String>(
          isDense: true,
          underline: const SizedBox.shrink(),
          value: exists ? current : null,
          hint: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 150),
            child: Text(
              current.isEmpty ? I18n.resource_role_unknown.tr : _shortLabel,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
            ),
          ),"""
new = """        DropdownButton<String>(
          isDense: true,
          underline: const SizedBox.shrink(),
          value: exists ? current : null,
          hint: ConstrainedBox(
            // 三行布局后本行独占宽度，240 足够放「名字 (Lv60 · 服务器)」
            constraints: const BoxConstraints(maxWidth: 240),
            child: Text(
              current.isEmpty ? I18n.resource_role_unknown.tr : _shortLabel,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
            ),
          ),"""
if old in text:
    text = text.replace(old, new)
    print("hint widened + uses shortLabel")
else:
    print("hint anchor NOT found")

io.open(F, "w", encoding="utf-8", newline="").write(text)
