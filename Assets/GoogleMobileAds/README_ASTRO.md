# Google Mobile Ads — Editor, Android e iOS

Componentes do Google Mobile Ads Unity Plugin **11.5.0**, publicados pelo Google e obtidos do registro OpenUPM indicado na documentação oficial:

- https://developers.google.com/admob/unity/quick-start
- https://developers.google.com/admob/unity/test-ads
- https://github.com/googleads/googleads-mobile-unity/releases/tag/v11.5.0
- https://package.openupm.com/com.google.ads.mobile/-/com.google.ads.mobile-11.5.0.tgz

SHA-512 do arquivo original, conferido contra o registro:
`348jMWb3jw9IFpnRXS4PlbJleOWIGgyJ7cZvHaUXfrlb50smYQ4wSsCEF6hRW8XdS2tETS3pqLUZLfCMy+BJJA==`

Importados os assemblies gerenciados e `Editor/Resources` (prefabs, imagens e respectivos GUIDs), sem alterar intencionalmente os visuais do SDK. A Unity migrou a serialização e as referências UGUI de `BANNER.prefab` e `Rewarded/1024x768.prefab`; referências de imagens/textos preservadas. A licença Apache 2.0 acompanha a distribuição em `LICENSE`.

Integração mobile em 21/10/2026: os assemblies comuns suportam Editor, Android e iOS; clientes Android/iOS entram somente na plataforma correspondente e no Editor. Os dois clientes Unity e os placeholders continuam somente Editor. O serviço `AndroidAds.Google.cs` usa a mesma API oficial nas três plataformas; `AstroAds.java` é legado e não é mais instanciado.

Complementos obtidos do pacote oficial de mesma versão:
https://github.com/googleads/googleads-mobile-unity/releases/download/v11.5.0/GoogleMobileAds-v11.5.0.unitypackage

SHA-256 do `.unitypackage`: `770c34bb7485e8db20a888cc008a14fdc94ffe8d34b4d2ee24434a7be91e08ce`.

Importados `googlemobileads-unity.aar`, `unity-plugin-library.xcframework`, templates/headers iOS, `link.xml`, lista SKAdNetwork e External Dependency Manager 1.2.188. Importadores nativos adaptados para o formato 2 da Unity 6.6, com plataformas explícitas. O xcframework é o plugin iOS; seus arquivos internos não são plugins independentes. Nenhum postprocessador de build Google foi importado.

Android mantém AdMob 25.5.0/UMP 4.0.0 no Gradle e acrescenta dependências AndroidX exigidas pelo adaptador. iOS usa EDM/CocoaPods com Google-Mobile-Ads-SDK ~> 13.9 e GoogleUserMessagingPlatform 3.1.0 (`AstroAdsDependencies.xml`). O postprocessador próprio `IosAdsBuildConfig` escreve App ID, SKAdNetworkItems e a descrição para eventual solicitação ATT via UMP, preservando descrição existente; não solicita ATT por conta própria. Requer módulo Unity iOS Build Support e CocoaPods para exportar/compilar.

Editor e Development Builds usam IDs oficiais de teste. Release usa `AndroidAds.asset` quando `useTestAds` está desligado. O Editor mostra somente placeholders oficiais. Não importar outro SDK completo sobre esta seleção: conferir duplicatas, dependências, importadores e callbacks antes de atualizar. Testar Domain Reload desativado e build em cada plataforma.
