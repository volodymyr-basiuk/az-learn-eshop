using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Azure.Messaging.ServiceBus;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Configuration;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class OrderService : IOrderService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IUriComposer _uriComposer;
    private readonly IRepository<Basket> _basketRepository;
    private readonly IRepository<CatalogItem> _itemRepository;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public OrderService(IRepository<Basket> basketRepository,
        IRepository<CatalogItem> itemRepository,
        IRepository<Order> orderRepository,
        IUriComposer uriComposer,
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _orderRepository = orderRepository;
        _uriComposer = uriComposer;
        _basketRepository = basketRepository;
        _itemRepository = itemRepository;
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task CreateOrderAsync(int basketId, Address shippingAddress)
    {
        var basketSpec = new BasketWithItemsSpecification(basketId);
        var basket = await _basketRepository.GetBySpecAsync(basketSpec);

        Guard.Against.NullBasket(basketId, basket);
        Guard.Against.EmptyBasketOnCheckout(basket.Items);

        var catalogItemsSpecification = new CatalogItemsSpecification(basket.Items.Select(item => item.CatalogItemId).ToArray());
        var catalogItems = await _itemRepository.ListAsync(catalogItemsSpecification);

        var items = basket.Items.Select(basketItem =>
        {
            var catalogItem = catalogItems.First(c => c.Id == basketItem.CatalogItemId);
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            var orderItem = new OrderItem(itemOrdered, basketItem.UnitPrice, basketItem.Quantity);
            return orderItem;
        }).ToList();

        decimal finalPrice = 0;
        items.ForEach(i =>
        {
            finalPrice += i.UnitPrice * i.Units;
        });
        var order = new Order(basket.BuyerId, shippingAddress, items, finalPrice);

        await _orderRepository.AddAsync(order);
        var orderBody = JsonSerializer.Serialize(order);


        // connection string to your Service Bus namespace
        string sbConnectionString = _configuration["AzOrderServiceBus"];
        // name of your Service Bus queue
        string queueName = "order";
        var clientOptions = new ServiceBusClientOptions()
        {
            TransportType = ServiceBusTransportType.AmqpWebSockets,
            RetryOptions = new ServiceBusRetryOptions { MaxRetries = 3 }
        };
        var client = new ServiceBusClient(sbConnectionString, clientOptions);
        var sender = client.CreateSender(queueName);

        try
        {
            // Use the producer client to send the batch of messages to the Service Bus queue
            await sender.SendMessageAsync(new ServiceBusMessage(orderBody));
        }
        finally
        {
            // Calling DisposeAsync on client types is required to ensure that network
            // resources and other unmanaged objects are properly cleaned up.
            await sender.DisposeAsync();
            await client.DisposeAsync();
        }

        var httpContent = new StringContent(orderBody);
        // send to blob by Azure function:
        _httpClient.BaseAddress = new System.Uri("https://eshopdeliveryorderprocessor.azurewebsites.net/");
        var result = await _httpClient.PostAsync("api/HttpTrigger1?code=iFLek_VGyYOZ4tx6y3KcigwAK9TEVAMKOyCVH9QyQX2kAzFutEG9hQ==", httpContent);
        try
        {
            result.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
        }
    }
}
