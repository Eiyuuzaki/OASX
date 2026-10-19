// ignore_for_file: non_constant_identifier_names
library i18n;

import 'package:get/get.dart';

import 'package:oasx/utils/extension_utils.dart';

import 'i18n_content.dart';
part 'i18n_us.dart';
part 'cn_parts/cn_ui.dart';
part 'cn_parts/cn_menu.dart';
part 'cn_parts/cn_script.dart';
part 'cn_parts/cn_restart.dart';
part 'cn_parts/cn_global_game.dart';
part 'cn_parts/cn_raid_config.dart';
part 'cn_parts/cn_invite_config.dart';
part 'cn_parts/cn_general_battle_config.dart';
part 'cn_parts/cn_switch_soul.dart';
part 'cn_parts/cn_orochi_config.dart';
part 'cn_parts/cn_sougenbi_config.dart';
part 'cn_parts/cn_fallen_sun_config.dart';
part 'cn_parts/cn_eternity_sea_config.dart';
part 'cn_parts/cn_other_world_twilight_config.dart';
part 'cn_parts/cn_dragon_spine_config.dart';
part 'cn_parts/cn_area_boss_config.dart';
part 'cn_parts/cn_gold_youkai_config.dart';
part 'cn_parts/cn_nian_config.dart';
part 'cn_parts/cn_talisman_config.dart';
part 'cn_parts/cn_pets_config.dart';
part 'cn_parts/cn_simple_tidy_config.dart';
part 'cn_parts/cn_delegation_config.dart';
part 'cn_parts/cn_wanted_quests_config.dart';
part 'cn_parts/cn_tako_config.dart';
part 'cn_parts/cn_bondling_config.dart';
part 'cn_parts/cn_evo_zone_config.dart';
part 'cn_parts/cn_goryou_config.dart';
part 'cn_parts/cn_exploration_config.dart';
part 'cn_parts/cn_utilize_config.dart';
part 'cn_parts/cn_activation_config.dart';
part 'cn_parts/cn_realm_raid_config.dart';
part 'cn_parts/cn_missions_config.dart';
part 'cn_parts/cn_hunt_config.dart';
part 'cn_parts/cn_true_orochi_config.dart';
part 'cn_parts/cn_rich_man_config.dart';
part 'cn_parts/cn_secret_config.dart';
part 'cn_parts/cn_trifles_config.dart';
part 'cn_parts/cn_shop_config.dart';
part 'cn_parts/cn_duel_config.dart';
part 'cn_parts/cn_general_climb_config.dart';
part 'cn_parts/cn_meta_demon_config.dart';
part 'cn_parts/cn_hyakkiyakou_config.dart';
part 'cn_parts/cn_kokan_config.dart';
part 'cn_parts/cn_six_realms_config.dart';
part 'cn_parts/cn_frog_boss_config.dart';
part 'cn_parts/cn_float_parade_config.dart';
part 'cn_parts/cn_quiz_config.dart';
part 'cn_parts/cn_herotest_config.dart';
part 'cn_parts/cn_abyss_shadows_config.dart';
part 'cn_parts/cn_find_jade_config.dart';
part 'cn_parts/cn_guild_banquet_config.dart';
part 'cn_parts/cn_kitty_shop_config.dart';
part 'cn_parts/cn_experience_youkai_config.dart';
part 'cn_parts/cn_demon_encounter_config.dart';
part 'cn_parts/cn_memory_scrolls_config.dart';
part 'cn_parts/cn_demon_retreat_config.dart';
part 'cn_parts/cn_dye_trials_config.dart';

class Messages extends Translations {
  @override
  Map<String, Map<String, String>> get keys {
    return {
      'en_US': all_us_translate,
      'zh_CN': all_cn_translate,
    };
  }

  late final Map<String, String> all_us_translate = {
    ..._us_base_map,
    I18n.homeTasksTab: 'Tasks',
    I18n.homeStatsTab: 'Stats',
    I18n.homeNoScriptSelected: 'No script selected',
    ...convertCnToUsMap()
  };

  /// Extract the keys from the Chinese map, remove underscores, and capitalize the first letter of each word to use as the English translation
  Map<String, String> convertCnToUsMap() {
    return all_cn_translate.entries
        .where((e) => !_us_base_map.containsKey(e.key))
        .map((e) => MapEntry(
              e.key,
              e.key.replaceAll('_', ' ').upperFirstWord(),
            ))
        .toMap();
  }

  late final Map<String, String> all_cn_translate = {
    ..._cn_ui,
    ..._cn_menu,
    ..._cn_script,
    ..._cn_restart,
    ..._cn_global_game,
    ..._cn_raid_config,
    ..._cn_invite_config,
    ..._cn_general_battle_config,
    ..._cn_switch_soul,
    ..._cn_orochi_config,
    ..._cn_sougenbi_config,
    ..._cn_fallen_sun_config,
    ..._cn_eternity_sea_config,
    ..._cn_other_world_twilight_config,
    ..._cn_dragon_spine_config,
    ..._cn_area_boss_config,
    ..._cn_gold_youkai_config,
    ..._cn_nian_config,
    ..._cn_talisman_config,
    ..._cn_pets_config,
    ..._cn_simple_tidy_config,
    ..._cn_delegation_config,
    ..._cn_wanted_quests_config,
    ..._cn_tako_config,
    ..._cn_bondling_config,
    ..._cn_evo_zone_config,
    ..._cn_goryou_config,
    ..._cn_exploration_config,
    ..._cn_utilize_config,
    ..._cn_activation_config,
    ..._cn_realm_raid_config,
    ..._cn_missions_config,
    ..._cn_hunt_config,
    ..._cn_true_orochi_config,
    ..._cn_rich_man_config,
    ..._cn_secret_config,
    ..._cn_trifles_config,
    ..._cn_shop_config,
    ..._cn_duel_config,
    ..._cn_general_climb_config,
    ..._cn_meta_demon_config,
    ..._cn_hyakkiyakou_config,
    ..._cn_kokan_config,
    ..._cn_six_realms_config,
    ..._cn_frog_boss_config,
    ..._cn_float_parade_config,
    ..._cn_quiz_config,
    ..._cn_herotest_config,
    ..._cn_abyss_shadows_config,
    ..._cn_find_jade_config,
    ..._cn_guild_banquet_config,
    ..._cn_kitty_shop_config,
    ..._cn_experience_youkai_config,
    ..._cn_demon_encounter_config,
    ..._cn_memory_scrolls_config,
    ..._cn_demon_retreat_config,
    ..._cn_dye_trials_config,
    // 资产页（Resource）
    I18n.menu_resource: '资产',
    I18n.resource_title: '资产',
    I18n.resource_updated_at: '更新于',
    I18n.resource_refresh: '刷新',
    I18n.resource_collect_now: '立即采集',
    I18n.resource_collecting: '采集中',
    I18n.resource_collect_ok: '快照已保存',
    I18n.resource_collect_failed: '采集失败。请检查 frida-server 与游戏是否在运行。',
    I18n.resource_collector_on: '自动采集中',
    I18n.resource_collector_off: '未在采集',
    I18n.resource_empty: '还没有快照。点「立即采集」从游戏读取资源。',
    I18n.resource_close: '关闭',
    I18n.resource_no_history: '还没有历史数据',
    I18n.resource_role_hint: '数据按角色隔离，切换账号不会混记。',
    I18n.resource_role_unknown: '未知角色',
    I18n.resource_vs_yesterday: '比昨日',
    I18n.resource_vs_earlier: '比此前',
  };
}
