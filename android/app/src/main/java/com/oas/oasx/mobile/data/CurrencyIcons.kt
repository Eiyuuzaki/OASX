package com.oas.oasx.mobile.data

import android.content.Context

/// 货币图标白名单（对齐 WPF CurrencyIconRegistry）。
///
/// 后端返回 190+ 项资源，但本地只有 assets/currency/ 下十几个图标——
/// 只展示"有图标"的项，其余（活动代币、过期材料等）一律过滤掉。
/// 白名单直接枚举 assets 目录，与 WPF 行为一致（新增图标即自动纳入）。
object CurrencyIcons {
    @Volatile
    private var cache: Set<String>? = null

    /// assets/currency/ 下的 basename 集合（不含 .png）。
    fun get(context: Context): Set<String> {
        cache?.let { return it }
        val set = try {
            context.assets.list("currency")
                ?.mapNotNull { name ->
                    if (!name.endsWith(".png", ignoreCase = true)) null
                    else name.substring(0, name.length - 4).lowercase()
                }
                ?.toSet() ?: emptySet()
        } catch (e: Exception) {
            emptySet()
        }
        cache = set
        return set
    }

    /// 后端 icon 可能是 icon_jinbi 或 icon_jinbi.png → 统一 basename。
    fun baseName(icon: String): String =
        icon.trim().let {
            if (it.endsWith(".png", ignoreCase = true)) it.substring(0, it.length - 4) else it
        }

    fun has(context: Context, icon: String): Boolean =
        get(context).contains(baseName(icon).lowercase())
}

/// 资产卡片聚合（对齐 WPF ResourcePage.AggregateCards）。
///
/// 三道过滤：
/// 1. 图标白名单 + (count>0 或 delta≠0)——账本里"花光"是重要事件不能丢
/// 2. (图标,名称) 聚合——同名视为同一种资产的多期版本，数量相加
/// 3. 按图标二次去重——同一图标多期/多名称只留一张卡
///    （优先常驻档 id<901000 → 数量大 → id 小）
object ResourceAggregator {
    /// 常驻资源 id 上界（大于它的多是活动代币/过期材料）。
    private const val CORE_ID_CUTOFF = 901_000

    data class Card(
        val id: String,
        val name: String,
        val icon: String,
        val count: Long,
        val delta: Long,
    )

    private fun idOf(id: String): Int = id.toIntOrNull() ?: Int.MAX_VALUE

    private fun prefer(a: Card, b: Card): Boolean {
        val aCore = idOf(a.id) < CORE_ID_CUTOFF
        val bCore = idOf(b.id) < CORE_ID_CUTOFF
        if (aCore != bCore) return aCore
        if (a.count != b.count) return a.count > b.count
        return idOf(a.id) < idOf(b.id)
    }

    /// 入参为后端原始项（icon 为后端名，可能是 xxx.png）。
    fun aggregate(
        context: Context,
        raw: List<Triple<String, String, Long>>, // (id, name, count)
        deltas: Map<String, Long> = emptyMap(),  // name -> delta
        icons: Map<String, String> = emptyMap(), // name -> icon
    ): List<Card> {
        // 1) 白名单 + 有效值
        val filtered = raw.filter { (id, name, count) ->
            val icon = icons[name] ?: ""
            val delta = deltas[name] ?: 0L
            CurrencyIcons.has(context, icon) && (count > 0 || delta != 0L)
        }

        // 2) (icon, name) 聚合
        val byIconName = filtered
            .groupBy { (id, name, _) ->
                val icon = CurrencyIcons.baseName(icons[name] ?: "").lowercase()
                icon to name
            }
            .map { (key, group) ->
                val (icon, name) = key
                Card(
                    id = group.maxByOrNull { idOf(it.first) }?.first ?: "",
                    name = name,
                    icon = icon,
                    count = group.sumOf { it.third },
                    delta = group.sumOf { deltas[it.second] ?: 0L },
                )
            }

        // 3) 按图标二次去重
        val byIcon = LinkedHashMap<String, Card>()
        for (agg in byIconName) {
            val icon = agg.icon
            val prev = byIcon[icon]
            if (prev == null || prefer(agg, prev)) {
                byIcon[icon] = agg
            }
        }

        // 排序：|delta| 降序 → 名称
        return byIcon.values.sortedWith(
            compareByDescending<Card> { kotlin.math.abs(it.delta) }
                .thenBy { it.name },
        )
    }
}