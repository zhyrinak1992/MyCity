# My City — план первой сборки для iPhone

## Цель
Получить первую тестовую сборку My City для iPhone 16 Pro, установить её через AltStore, затем продолжить разработку и обновить IPA.

## Важное ограничение
Windows может подготовить Unity-проект и экспортировать Xcode-проект при установленном модуле iOS Build Support. Финальный IPA требует Xcode на macOS. Unity подтверждает два этапа iOS-сборки: Unity экспортирует Xcode-проект, затем Xcode собирает приложение. Если Mac нет, нужен Unity Build Automation или другой облачный Mac-сервис.

## Что добавлено в проект v1.6.67
- Editor-команда `My City > iOS > 1 - Prepare iPhone project`.
- Создание стартовой сцены `Assets/Scenes/MyCity_Main.unity`.
- Добавление стартовой сцены в Build Settings.
- Базовые iOS Player Settings: имя My City, Bundle ID `com.mycity.simulator`, альбомная ориентация, iOS 15+, IL2CPP.
- Editor-команда `My City > iOS > 2 - Export Xcode project` для экспорта Xcode-проекта на машине с установленным iOS Build Support.

## Что необходимо сделать на Windows
1. Открыть именно проект v1.6.67 в Unity 6000.3.29f1.
2. Если Unity Hub предложит установить модуль iOS Build Support — установить его для этой версии Editor.
3. В меню Unity выбрать `My City > iOS > 1 - Prepare iPhone project`.
4. Проверить Console на ошибки компиляции C#.
5. Сохранить проект и отправить его в Git-репозиторий, подключённый к Unity Dashboard / Build Automation.

## Облачная сборка
В Unity Dashboard создать проект/организацию или открыть существующий проект, включить Build Automation, подключить репозиторий, выбрать ветку и Unity 6000.3.29f1, затем создать iOS-конфигурацию. Точные пункты интерфейса могут отличаться. Для iOS Build Automation потребуется настроить Apple signing/credentials, если сервис должен выдать устанавливаемый IPA. Не передавать Apple ID/пароль в чат.

## Установка через AltStore
После получения IPA: открыть AltServer на Windows, подключить iPhone, выбрать AltStore/установку IPA по актуальной инструкции AltStore. Для бесплатной подписи приложения потребуется периодическое обновление подписи (обычно раз в 7 дней); AltStore указывает лимит трёх sideloaded apps для бесплатной учётной записи. На iOS 16+ может потребоваться Developer Mode.

## Что не заявляется как готовое
- В этом пакете нет IPA.
- Облачная сборка не запускалась: она требует авторизации в Unity Dashboard, подключённого репозитория и настроек Apple signing.
- Unity Editor не запускался в этой среде, поэтому C#-компиляция и фактический экспорт не подтверждены.
- Тестирование на iPhone не выполнялось.

## Ссылки на официальные инструкции
- Unity iOS build process: https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/iphone/ios-building-and-delivering/build-process
- Unity iOS Build Automation: https://docs.unity.com/en-us/build-automation/basic-build-configuration/set-up-an-ios-build-configuration
- AltStore Windows: https://faq.altstore.io/altstore-classic/how-to-install-altstore-windows
