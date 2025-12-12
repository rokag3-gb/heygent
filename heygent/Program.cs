using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using heygent.Core;
using heygent.Core.Helper;
using heygent.Core.Ipc;
using Serilog;

namespace heygent;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            // ASCII Art 출력
            Console.WriteLine(new string('=', 54));
            Console.WriteLine("");
            Console.WriteLine(@"$$\   $$\ $$$$$$$$\ $$\     $$\                              $$\     ");
            Console.WriteLine(@"$$ |  $$ |$$  _____|\$$\   $$  |                             $$ |    ");
            Console.WriteLine(@"$$ |  $$ |$$ |       \$$\ $$  /$$$$$$\   $$$$$$\  $$$$$$$\ $$$$$$\   ");
            Console.WriteLine(@"$$$$$$$$ |$$$$$\      \$$$$  /$$  __$$\ $$  __$$\ $$  __$$\\_$$  _|  ");
            Console.WriteLine(@"$$  __$$ |$$  __|      \$$  / $$ /  $$ |$$$$$$$$ |$$ |  $$ | $$ |    ");
            Console.WriteLine(@"$$ |  $$ |$$ |          $$ |  $$ |  $$ |$$   ____|$$ |  $$ | $$ |$$\ ");
            Console.WriteLine(@"$$ |  $$ |$$$$$$$$\     $$ |  \$$$$$$$ |\$$$$$$$\ $$ |  $$ | \$$$$  |");
            Console.WriteLine(@"\__|  \__|\________|    \__|   \____$$ | \_______|\__|  \__|  \____/ ");
            Console.WriteLine(@"                              $$\   $$ |                             ");
            Console.WriteLine(@"                              \$$$$$$  |                             ");
            Console.WriteLine(@"                               \______/                              ");
            Console.WriteLine("");
            Console.WriteLine(new string('=', 54));
            Console.WriteLine("");

            // Serilog 설정
            // 최대 50MB * 최대 60개 파일 = 최대 3GB
            // logs 폴더의 용량이 최대 3GB 선에서 유지됨
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u4}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
                )
                .WriteTo.File(
                    path: "logs/heygent-.log",
                    // path: $"logs/{DateTime.Now:yyyy-MM-dd}-heygent.log",
                    rollingInterval: RollingInterval.Day,
                    rollOnFileSizeLimit: true,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u4}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                    fileSizeLimitBytes: 50 * 1024 * 1024, // 50MB 초과하면 새 파일 생성.
                    retainedFileCountLimit: 60, // 매일 1개 로그 파일 생성. 즉 최대 60일간 보관. 실시간 삭제는 아니고, 새 로그 파일 생성 시 체크.
                    buffered: false // 권장: 즉시쓰기. 메모리 버퍼 최소화
                )
                .CreateLogger();

            // arguments 통해서 버전정보 출력
            if (args is not null && args.Length > 0)
            {
                if (args.Any(arg =>
                    arg == "-v" || arg == "-ver" || arg == "-version"
                    || arg == "-i" || arg == "-info"
                    || arg == "-h" || arg == "-help")
                    )
                {
                    try
                    {
                        Console.WriteLine(
                            //$"Title: {AssemblyInfo.Title}\n" +
                            $"Product: {AssemblyInfo.Product}\n" +
                            $"Version: {AssemblyInfo.HeadVer} ({AssemblyInfo.InformationVersion})\n" +
                            $"Description: {AssemblyInfo.Description}\n" +
                            $"Company: {AssemblyInfo.Company}\n" +
                            $"Manager: {AssemblyInfo.Manager}\n" +
                            $"{AssemblyInfo.Copyright}\n"
                        );
                        Console.WriteLine(new string('=', 54));
                        Console.WriteLine("");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"버전 정보를 읽는 중 오류 발생: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"Unknown Parameter. - 사용 가능한 파라미터: -v, -ver, -version, -i, -info, -h, -help");
                }

                return; // 즉시 종료
            }

            // 호스트 빌더 설정
            var host = Host.CreateDefaultBuilder(args)
                .UseSerilog() // 모든 Logger<T>는 Serilog을 사용
                .ConfigureServices(services =>
                {
                    /* [Service lifecycle]
                    * Transient: instance 요청 시마다 new instance가 생성된다. stateless service에 적합하다. multi-threading 시나리오에 가장 적합하다.
                    * Scoped: 클라이언트 요청 당 한번 생성된다. 동일한 클라이언트에 대해 같은 instance를 공유하고 싶을때 적합하다. (웹 애플리케이션에서 유용)
                    * Singleton: 처음 요청 시 생성되고 이 후 요청 시 동일한 instance를 사용한다. IoC container가 생성하고 app의 모든 requests와 함께 하나의 instance를 공유한다. 
                    */

                    services.AddSingleton<YamlConfigHelper>();
                    services.AddSingleton<NetInfoHelper>();

                    // 스케줄러 등록
                    services.AddHostedService<Scheduler.FlexSyncService>();
                    // services.AddHostedService<Scheduler.AgentNotificationService>();

                    //services.AddTransient<FileTransferService>();
                    //services.AddTransient<IFileTransferService>(provider =>
                    //{
                    //    var logger = provider.GetRequiredService<ILogger<FileTransferService>>();
                    //    var notifier = provider.GetRequiredService<Core.Notification.NotificationService>();
                    //    return new FileTransferService(logger, notifier);
                    //});
                    services.AddTransient<IFileService, FileService>();

                    services.AddTransient<Core.Notification.LarkNotificationSender>();
                    services.AddTransient<Core.Notification.EmailNotificationSender>();
                    services.AddTransient<Core.Notification.SmsNotificationSender>();

                    // Factory 및 Service 등록
                    services.AddSingleton<Core.Notification.INotificationSenderFactory, Core.Notification.NotificationSenderFactory>();
                    services.AddSingleton<Core.Notification.NotificationService>();

                    // Flex 관련 서비스 등록
                    services.AddHttpClient<Core.Flex.FlexApiClient>();
                    services.AddTransient<Core.Flex.FlexRepository>();
                    services.AddTransient<Core.Flex.FlexSyncManager>();

                    // Gemini API Client 등록
                    services.AddHttpClient<Core.Gemini.GeminiApiClient>();

                    // NamedPipe 서버 등록
                    services.AddSingleton<INamedPipeServer, NamedPipeServer>();
                })
                .Build();

            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation($"heygent {AssemblyInfo.HeadVer} Started! ({AssemblyInfo.InformationVersion})");

            // YAML 설정 파일 로드 -> new LoggerFactory()로 YamlConfigHelper을 직접 만들지 말고 DI에서 꺼내 쓰는 방식으로 변경
            var yamlConfigHelper = host.Services.GetRequiredService<YamlConfigHelper>();
            Conf.Current = yamlConfigHelper.Load();
            yamlConfigHelper = null; // 명시적 메모리 해제

            // NetInfo Snapshot 불러오기
            var netInfoHelper = host.Services.GetRequiredService<NetInfoHelper>();
            Conf.CurrentNetInfo = await netInfoHelper.SnapshotAsync();

            logger.LogInformation($"NET Info 불러오기 성공!");
            logger.LogInformation($"HostName = [{Conf.CurrentNetInfo.HostName}], Private IPv4 = [{Conf.CurrentNetInfo.PrivateIPv4}], Public IPv4 = [{Conf.CurrentNetInfo.PublicIPv4}], Private IPv6 = [{Conf.CurrentNetInfo.PrivateIPv6}]");

            // NamedPipe 서버 시작
            var namedPipeServer = host.Services.GetRequiredService<INamedPipeServer>();
            namedPipeServer.Start();

            ////////////////////////////////////////////////////
            // Gemini API Test
            try
            {
                var geminiClient = host.Services.GetRequiredService<Core.Gemini.GeminiApiClient>();

                // API Key가 설정되어 있는지 확인
                if (!string.IsNullOrWhiteSpace(Core.Credential.GeminiSecret.ApiKey))
                {
                    logger.LogInformation("Gemini API 테스트 호출을 시도합니다...");

                    // Get models
                    // try
                    // {
                    //     var models = await geminiClient.ListModelsAsync();
                    //     logger.LogInformation($"[Gemini Model List] 총 {models.Count}개의 모델을 찾았습니다.");
                    //     foreach (var model in models)
                    //     {
                    //         // generateContent 메소드를 지원하는 모델만 출력
                    //         if (model.SupportedGenerationMethods != null && model.SupportedGenerationMethods.Contains("generateContent"))
                    //         {
                    //             logger.LogInformation($" - Model: {model.Name} ({model.DisplayName})");
                    //         }
                    //     }
                    // }
                    // catch (Exception ex)
                    // {
                    //     logger.LogWarning($"모델 목록 조회 실패: {ex.Message}");
                    // }
                    
                    // Gemini API request & response test
                    var prompt = "현재 시점의 FED 기준금리, 한국 기준금리를 알려주고, 앞으로 1년 동안의 전망을 간단히 설명해줘.";
                    // prompt = "최신의 Gemini 모델을 호출하는 간단한 샘플 소스코드를 Rust로 짜줘.";

                    var response = await geminiClient.GenerateContentAsync(prompt);

                    logger.LogInformation($"Gemini API 응답:\n{response}");
                }
                else
                {
                    logger.LogWarning("Gemini API Key가 설정되지 않았습니다. 'heygent.Core/Credential/Secret.cs' 파일에서 API Key를 설정하면 테스트가 실행됩니다.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Gemini API 테스트 호출 중 오류가 발생했습니다.");
            }
            ////////////////////////////////////////////////////

            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "애플리케이션 시작 중 오류가 발생했습니다.");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}