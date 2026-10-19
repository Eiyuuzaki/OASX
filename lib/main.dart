import 'package:device_preview/device_preview.dart';
import 'package:flutter/foundation.dart' show kReleaseMode;
import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:get/get.dart';
import 'package:oasx/service/profile_service.dart';
import 'package:oasx/config/theme.dart'
    show amoledDarkTheme, darkTheme, lightTheme;
import 'package:oasx/modules/settings/controllers/settings_controller.dart';
import 'package:oasx/service/autostart_service.dart';
import 'package:oasx/service/app_update_service.dart';
import 'package:oasx/service/locale_service.dart';
import 'package:oasx/service/script_service.dart';
import 'package:oasx/service/system_tray_service.dart';
import 'package:oasx/service/theme_service.dart';
import 'package:oasx/service/websocket_service.dart';
import 'package:oasx/service/window_service.dart';
import 'package:oasx/service/acrylic_service.dart';
import 'package:oasx/translation/i18n.dart';
import 'package:oasx/utils/logger.dart';
import 'package:oasx/utils/platform_utils.dart';
import 'package:oasx/routes.dart';
import 'package:responsive_builder/responsive_builder.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // ★ 官方文档要求：Window.initialize() 必须在 runApp 之前调用。
  // 之前放在 initService 里（window_manager 初始化之后）会导致材质不生效。
  await AcrylicService.initializePlugin();

  await initService();

  runApp(
    DevicePreview(
      // DevicePreview 的模拟外壳与窗口透明冲突，材质模式下禁用
      enabled: !kReleaseMode &&
          PlatformUtils.isWindows &&
          !AcrylicService.transparentBackground,
      builder: (context) => const OASXApp(),
    ),
  );
}

class OASXApp extends StatelessWidget {
  const OASXApp({super.key});

  @override
  Widget build(BuildContext context) {
    final localeService = Get.find<LocaleService>();
    final themeService = Get.find<ThemeService>();

    return ResponsiveApp(
      builder: (context) {
        // Obx：主题模式/AMOLED 是 Rx，切换时需要重建 GetMaterialApp
        return Obx(() => GetMaterialApp(
          debugShowCheckedModeBanner: false,
          builder: DevicePreview.appBuilder,
          scrollBehavior: GlobalBehavior(),
          translations: Messages(),
          locale: localeService.currentLocale,
          fallbackLocale: localeService.fallbackLocale,
          title: 'OASX',
          initialRoute: Routes.initial,
          getPages: Routes.routes,
          theme: lightTheme,
          // AMOLED 开关开启时用纯黑深色主题
          darkTheme:
              themeService.isAmoled ? amoledDarkTheme : darkTheme,
          themeMode: themeService.themeMode,
        ));
      },
    );
  }
}

class GlobalBehavior extends MaterialScrollBehavior {
  @override
  Set<PointerDeviceKind> get dragDevices => {
    PointerDeviceKind.touch,
    PointerDeviceKind.mouse,
  };
}

Future<void> initService() async {
  await ProfileService.initialize();

  Get.put(SettingsController(), permanent: true);
  if (PlatformUtils.isDesktop) {
    Get.put(SystemTrayService(), permanent: true);
  }
  Get.lazyPut<WebSocketService>(() => WebSocketService(), fenix: true);
  final windowService = Get.put(WindowService());

  await Future.wait([
    initLogger(),
    Get.putAsync(() async => LocaleService()),
    Get.putAsync(() async => ThemeService()),
    Get.putAsync(() async => AutoStartService(), permanent: true),
    Get.putAsync(() async => AppUpdateService(), permanent: true),
    windowService.ready,
    Get.putAsync(() async => ScriptService(), permanent: true),
  ]);

  // 材质设置必须在 window_manager 完成所有窗口样式操作**之后**，
  // 否则会被 window_manager 的后续调用覆盖掉（表现为调用成功但无效果）。
  // 先读用户开关偏好（依赖 GetStorage，所以必须放在 ProfileService 之后）。
  AcrylicService.loadPreference();
  await AcrylicService.applyEffect(
    dark: Get.find<ThemeService>().isDarkMode,
  );
}
