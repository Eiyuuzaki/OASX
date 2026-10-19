# -*- coding: utf-8 -*-
"""角色选择器：宽限宽 + 两行换行（不省略），完整显示「永远的平安京」。

三行布局下行1 独占整行（最小 360px）：
  收起态 hint 限宽 320（360 - icon 22 - gap 4 - (2) 24 - padding 余量）
  显示名放不下时 maxLines:2 换行，不丢字。
"""
import io
from pathlib import Path

F = Path(r"D:\OAS-Kirin\OASX\lib\views\resource\resource_view.dart")
text = io.open(F, encoding="utf-8").read()

old = """        DropdownButton<String>(
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
          ),
          items: roles
              .map((r) => DropdownMenuItem(
                    value: r.roleKey,
                    child: ConstrainedBox(
                      constraints: const BoxConstraints(maxWidth: 220),
                      child: Text(r.displayLabel,
                          maxLines: 1, overflow: TextOverflow.ellipsis),
                    ),
                  ))
              .toList(),"""
new = """        DropdownButton<String>(
          isDense: true,
          underline: const SizedBox.shrink(),
          value: exists ? current : null,
          hint: ConstrainedBox(
            // 三行布局下行1 独占 360px：320 放得下
            // 「英雄崎凛 (Lv60 · 海外加速区)」；还放不下就换两行，不省略
            constraints: const BoxConstraints(maxWidth: 320),
            child: Text(
              current.isEmpty ? I18n.resource_role_unknown.tr : _shortLabel,
              maxLines: 2,
              overflow: TextOverflow.fade,
              softWrap: true,
            ),
          ),
          items: roles
              .map((r) => DropdownMenuItem(
                    value: r.roleKey,
                    child: ConstrainedBox(
                      constraints: const BoxConstraints(maxWidth: 320),
                      child: Text(r.displayLabel,
                          maxLines: 2, overflow: TextOverflow.fade),
                    ),
                  ))
              .toList(),"""
if old in text:
    text = text.replace(old, new)
    print("role selector widened (320px, 2 lines)")
else:
    print("anchor NOT found")

io.open(F, "w", encoding="utf-8", newline="").write(text)
