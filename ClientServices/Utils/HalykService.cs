using ClientService.EF;
using ClientService.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Net.Http;
using HtmlAgilityPack;
using Analyst.Utils;
using System.Net;


namespace ClientService.Utils
{

    public class HalykService
    {
        private readonly ApplicationDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly ILogger<HalykService> _logger;
        private readonly List<ProxyClient> _proxyClients = new();

        public HalykService(HttpClient httpClient, ILogger<HalykService> logger, ApplicationDbContext context)
        {
            _httpClient = httpClient;
            _logger = logger;
            _context = context;
            InitializeClients();
        }


        public void InitializeClients()
        {
            var handler1 = new HttpClientHandler
            {
                Proxy = new WebProxy("http://46.8.31.67:50100")
                {
                    Credentials = new NetworkCredential("suraubaev004", "KISYShQTrn")
                },
                UseProxy = true
            };

            var handler2 = new HttpClientHandler
            {
                Proxy = new WebProxy("http://109.248.199.99:50100")
                {
                    Credentials = new NetworkCredential("suraubaev004", "KISYShQTrn")
                },
                UseProxy = true
            };

            var handler3 = new HttpClientHandler
            {
                Proxy = new WebProxy("http://188.130.160.122:50100")
                {
                    Credentials = new NetworkCredential("suraubaev004", "KISYShQTrn")
                },
                UseProxy = true
            };

            _proxyClients.Add(new ProxyClient { Client = new HttpClient(handler1), Name = "Proxy1" });
            _proxyClients.Add(new ProxyClient { Client = new HttpClient(handler2), Name = "Proxy2" });
            _proxyClients.Add(new ProxyClient { Client = new HttpClient(handler3), Name = "Proxy3" });
        }

        private async Task<ProxyClient> GetAvailableClientAsync()
        {
            while (true)
            {
                foreach (var client in _proxyClients)
                {
                    await client.Semaphore.WaitAsync();
                    try
                    {
                        if (client.RequestCount < 6) return client;

                        if ((DateTime.UtcNow - client.LastUsed).TotalSeconds >= 35)
                        {
                            client.RequestCount = 0;
                            _logger.LogInformation("Выбран прокси: {ProxyName}, RequestCount: {Count}", client.Name, client.RequestCount);

                            return client;
                        }
                    }
                    finally
                    {
                        client.Semaphore.Release();
                    }
                }

                // Если все заняты — подождать 1 секунду и снова попробовать
                await Task.Delay(1000);
            }
        }
        private async Task<HttpResponseMessage?> SendWithRateLimitAsync(string url, string token)
        {
            var client = await GetAvailableClientAsync();

            await client.Semaphore.WaitAsync();
            try
            {
                _logger.LogInformation("Отправка через прокси: {ProxyName}, URL: {Url}", client.Name, url);

                var request = new HttpRequestMessage(HttpMethod.Get, url);

                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                var response = await client.Client.SendAsync(request);
                client.RequestCount++;

                if (client.RequestCount >= 5)
                {
                    client.LastUsed = DateTime.UtcNow;
                }

                return response;
            }
            finally
            {
                client.Semaphore.Release();
            }
        }



        public async Task<string?> LoginAsync(string email, string password)
        {
            var url = "https://halykmarket.kz/gw/merchant-orders/auth";
            var payload = new LoginRequest
            {
                Email = email,
                Password = password
            };

            try
            {
                var response = await _httpClient.PostAsJsonAsync(url, payload);

                _logger.LogInformation("Status: {StatusCode}", response.StatusCode);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadFromJsonAsync<LoginResponse>();
                    _logger.LogInformation("Token: {Token}", data?.Token);
                    return data?.Token;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Ошибка авторизации: {Error}", error);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Ошибка при выполнении запроса: {Message}", ex.Message);
                return null;
            }
        }

        public async Task<List<Setting>> GetSellerProductsAsync(string email, string password, string phoneNumber, Guid userId)
        {
            var allProduct = new List<Setting>();

            try
            {
                var token = await LoginAsync(email, password);
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogError("Не удалось получить токен для пользователя {Email}", email);
                    return new();
                }


                var headers = _httpClient.DefaultRequestHeaders;
                headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));


                var allProducts = new List<Dictionary<string, object>>();
                int page = 1;

                while (true)
                {
                    var url = $"https://halykmarket.kz/gw/merchant/merchant/product?q=&page={page}&status=ON_SALE";

                    try
                    {
                        var response = await _httpClient.GetAsync(url);
                        _logger.LogInformation("Страница {Page} - Status: {Status}", page, response.StatusCode);

                        if (!response.IsSuccessStatusCode)
                        {
                            var errorText = await response.Content.ReadAsStringAsync();
                            _logger.LogError("Ошибка при получении списка продуктов. URL: {Url}, Код: {Code}, Ответ: {Response}", url, response.StatusCode, errorText);
                            break;
                        }

                        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
                        if (data == null || !data.TryGetValue("content", out var contentObj))
                        {
                            _logger.LogWarning("Нет контента на странице {Page}.", page);
                            break;
                        }

                        var contentList = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(contentObj.ToString());
                        if (contentList == null || contentList.Count == 0)
                        {
                            _logger.LogInformation("Пустой список продуктов на странице {Page}", page);
                            break;
                        }

                        allProducts.AddRange(contentList);
                        page++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при запросе страницы {Page}: {Message}", page, ex.Message);
                        break;
                    }
                }

                foreach (var product in allProducts)
                {
                    string globalProductName = "";
                    string globalMarketUrl = "";

                    if (!product.TryGetValue("id", out var idObj))
                    {
                        _logger.LogWarning("Продукт без ID пропущен.");
                        continue;
                    }

                    var productId = idObj.ToString();
                    var fullInfoUrl = $"https://halykmarket.kz/gw/merchant/merchant/product/full-info?merchant_product_id={productId}&status=APPROVED&sub_status=ON_SALE";

                    try
                    {
                        var fullResp = await SendWithRateLimitAsync(fullInfoUrl, token);
                        if (!fullResp.IsSuccessStatusCode)
                        {
                            var err = await fullResp.Content.ReadAsStringAsync();
                            _logger.LogError("Ошибка получения full-info по ID {Id}. Код: {Code}, Ответ: {Resp}", productId, fullResp.StatusCode, err);
                            continue;
                        }

                        var fullInfo = await fullResp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
                        if (fullInfo == null || !fullInfo.ContainsKey("remainingInfo"))
                        {
                            _logger.LogWarning("Нет remainingInfo у продукта {Id}", productId);
                            continue;
                        }

                        var remainingInfoRaw = fullInfo["remainingInfo"].ToString();
                        var remainingInfo = JsonSerializer.Deserialize<Dictionary<string, object>>(remainingInfoRaw);

                        var merchantProductCode = remainingInfo["merchantProductCode"].ToString();
                        var loanPeriod = ((JsonElement)remainingInfo["loanPeriod"]).GetInt32();
                        var pointByCityRaw = remainingInfo["pointByCity"].ToString();
                        var pointByCity = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(pointByCityRaw);

                        foreach (var cityEntry in pointByCity)
                        {
                            var city = JsonSerializer.Deserialize<Dictionary<string, object>>(cityEntry["city"].ToString());
                            var points = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(cityEntry["points"].ToString());
                            var price = ((JsonElement)cityEntry["price"]).GetDouble();

                            foreach (var point in points)
                            {
                                var fullInfoEntity = new ProductPoint
                                {
                                    MerchantProductCode = merchantProductCode,
                                    LoanPeriod = loanPeriod,
                                    CityCode = ((JsonElement)city["code"]).GetString(),
                                    CityName = ((JsonElement)city["name"]).GetString(),
                                    CityNameRu = ((JsonElement)city["nameRu"]).GetString(),
                                    PointCode = ((JsonElement)point["code"]).GetString(),
                                    PointName = ((JsonElement)point["name"]).GetString(),
                                    Amount = ((JsonElement)point["amount"]).GetInt32(),
                                    Price = price
                                };

                                try
                                {
                                    var exists = _context.ProductPoints.FirstOrDefault(p => p.MerchantProductCode == fullInfoEntity.MerchantProductCode);
                                    if (exists != null) _context.ProductPoints.Remove(exists);
                                    _context.ProductPoints.Add(fullInfoEntity);
                                }
                                catch (Exception exDb)
                                {
                                    _logger.LogError(exDb, "Ошибка при обновлении ProductPoints для продукта {Code}", merchantProductCode);
                                }

                                if (!product.TryGetValue("name", out var nameObj)) continue;
                                var productName = nameObj.ToString();
                                globalProductName = productName;

                                if (!product.TryGetValue("imageUrl", out var imageUrlObj)) continue;
                                var productImageUrl = imageUrlObj.ToString();

                                if (!product.TryGetValue("marketUrl", out var marketUrlObj)) continue;
                                var productMarketUrl = marketUrlObj.ToString();
                                globalMarketUrl = productMarketUrl;

                                var userSettin = new UserSettings()
                                {
                                    UserId = userId,
                                    MerchantProductCode = fullInfoEntity.MerchantProductCode,
                                    ProductName = productName,
                                    lastMarketPrice = fullInfoEntity.Price,
                                    ActualPrice = fullInfoEntity.Price,
                                    ImageUrl = productImageUrl,
                                    MarketUrl = productMarketUrl,
                                    Remains = fullInfoEntity.Amount,
                                    IsDump = false,
                                    MaxPrice = 0,
                                    MinPrice = 0
                                };

                                try
                                {
                                    var existsSetting = _context.UserSettings.FirstOrDefault(p => p.MerchantProductCode == fullInfoEntity.MerchantProductCode);
                                    if (existsSetting != null) _context.UserSettings.Remove(existsSetting);
                                    else userSettin.fitstMarketPrice = userSettin.ActualPrice;

                                    _context.UserSettings.Add(userSettin);
                                    await _context.SaveChangesAsync();

                                    allProduct.Add(Mapper.ToSetting(userSettin));
                                }
                                catch (Exception exDb)
                                {
                                    _logger.LogError(exDb, "Ошибка при сохранении UserSettings для {Code}", fullInfoEntity.MerchantProductCode);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при обработке продукта ID {ProductId}: {Message}", productId, ex.Message);
                    }

                    try
                    {
                        globalMarketUrl = "https://halykmarket.kz" + globalMarketUrl;
                        var httpClient = new HttpClient();
                        httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

                        string html = await httpClient.GetStringAsync(globalMarketUrl);
                        var htmlDoc = new HtmlDocument();
                        htmlDoc.LoadHtml(html);

                        var productDesc = htmlDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'product-desc')]");
                        var vendorCodeDiv = productDesc?.SelectSingleNode(".//div[contains(@class, 'desc-vendor-code')]");

                        var spans = vendorCodeDiv?.SelectNodes(".//span");
                        if (spans != null && spans.Count >= 2)
                        {
                            var article = spans[1].InnerText.Trim();
                            _context.ProductArticuls.Add(new ProductArticuls() { Articule = article, productName = globalProductName });
                            await _context.SaveChangesAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при парсинге HTML артикула для продукта: {Url}", globalMarketUrl);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Фатальная ошибка в методе GetSellerProductsAsync: {Message}", ex.Message);
            }

            return allProduct;
        }


    }

    public class ProxyClient
    {
        public HttpClient Client { get; set; }
        public string Name { get; set; }
        public int RequestCount { get; set; }
        public DateTime LastUsed { get; set; } = DateTime.MinValue;
        public SemaphoreSlim Semaphore { get; set; } = new SemaphoreSlim(1, 1); // Защита от параллельных гонок
    }

}
