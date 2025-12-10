using Dapr.Client;
using FastFood.Common;
using FinanceService.Observability;
using Microsoft.Extensions.Logging;
using Moq;
using OrderPlacement.Services;
using OrderService.Models.Entities;
using OrderService.Models.Helpers;
using Xunit;

namespace OrderService.Unit.Tests.Services;

public class OrderProcessingServiceStateTests
{
    private readonly Mock<DaprClient> _daprClientMock;
    private readonly Mock<IOrderEventRouter> _orderEventRouterMock;
    private readonly Mock<IOrderServiceObservability> _observabilityMock;
    private readonly Mock<ILogger<OrderProcessingServiceState>> _loggerMock;
    private readonly OrderProcessingServiceState _service;

    public OrderProcessingServiceStateTests()
    {
        _daprClientMock = new Mock<DaprClient>();
        _orderEventRouterMock = new Mock<IOrderEventRouter>();
        _observabilityMock = new Mock<IOrderServiceObservability>();
        _loggerMock = new Mock<ILogger<OrderProcessingServiceState>>();
        
        _service = new OrderProcessingServiceState(
            _daprClientMock.Object,
            _orderEventRouterMock.Object,
            _observabilityMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task AddItem_WhenProductDoesNotExist_AddsNewItem()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            State = OrderState.Creating,
            Items = new List<OrderItem>()
        };

        var newItem = new OrderItem
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Quantity = 2,
            ItemPrice = 6.99m,
            ProductDescription = "Cheeseburger"
        };

        _daprClientMock
            .Setup(x => x.GetStateAsync<Order>(
                FastFoodConstants.StateStoreName,
                $"OrderProcessing-{orderId}",
                It.IsAny<ConsistencyMode?>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        await _service.AddItem(orderId, newItem);

        // Assert
        Assert.Single(order.Items);
        Assert.Equal(productId, order.Items.First().ProductId);
        Assert.Equal(2, order.Items.First().Quantity);
        
        _daprClientMock.Verify(x => x.SaveStateAsync(
            FastFoodConstants.StateStoreName,
            $"OrderProcessing-{orderId}",
            order,
            It.IsAny<StateOptions>(),
            It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        
        _daprClientMock.Verify(x => x.PublishEventAsync(
            FastFoodConstants.PubSubName,
            FastFoodConstants.EventNames.OrderUpdated,
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddItem_WhenProductAlreadyExists_ConsolidatesQuantity()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var existingItemId = Guid.NewGuid();
        
        var existingItem = new OrderItem
        {
            Id = existingItemId,
            ProductId = productId,
            Quantity = 1,
            ItemPrice = 6.99m,
            ProductDescription = "Cheeseburger"
        };

        var order = new Order
        {
            Id = orderId,
            State = OrderState.Creating,
            Items = new List<OrderItem> { existingItem }
        };

        var newItem = new OrderItem
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Quantity = 2,
            ItemPrice = 6.99m,
            ProductDescription = "Cheeseburger"
        };

        _daprClientMock
            .Setup(x => x.GetStateAsync<Order>(
                FastFoodConstants.StateStoreName,
                $"OrderProcessing-{orderId}",
                It.IsAny<ConsistencyMode?>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        await _service.AddItem(orderId, newItem);

        // Assert
        Assert.Single(order.Items);
        Assert.Equal(productId, order.Items.First().ProductId);
        Assert.Equal(3, order.Items.First().Quantity);
        Assert.Equal(existingItemId, order.Items.First().Id);
        
        _daprClientMock.Verify(x => x.SaveStateAsync(
            FastFoodConstants.StateStoreName,
            $"OrderProcessing-{orderId}",
            order,
            It.IsAny<StateOptions>(),
            It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        
        _daprClientMock.Verify(x => x.PublishEventAsync(
            FastFoodConstants.PubSubName,
            FastFoodConstants.EventNames.OrderUpdated,
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddItem_WithMultipleDifferentProducts_AddsAllAsSeparateItems()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var product1Id = Guid.NewGuid();
        var product2Id = Guid.NewGuid();
        
        var existingItem = new OrderItem
        {
            Id = Guid.NewGuid(),
            ProductId = product1Id,
            Quantity = 1,
            ItemPrice = 6.99m,
            ProductDescription = "Cheeseburger"
        };

        var order = new Order
        {
            Id = orderId,
            State = OrderState.Creating,
            Items = new List<OrderItem> { existingItem }
        };

        var newItem = new OrderItem
        {
            Id = Guid.NewGuid(),
            ProductId = product2Id,
            Quantity = 1,
            ItemPrice = 5.49m,
            ProductDescription = "Veggie Burger"
        };

        _daprClientMock
            .Setup(x => x.GetStateAsync<Order>(
                FastFoodConstants.StateStoreName,
                $"OrderProcessing-{orderId}",
                It.IsAny<ConsistencyMode?>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        await _service.AddItem(orderId, newItem);

        // Assert
        Assert.Equal(2, order.Items.Count);
        Assert.Contains(order.Items, i => i.ProductId == product1Id && i.Quantity == 1);
        Assert.Contains(order.Items, i => i.ProductId == product2Id && i.Quantity == 1);
    }

    [Fact]
    public async Task AddItem_WhenOrderNotInCreatingState_ThrowsInvalidOperationException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            State = OrderState.Confirmed,
            Items = new List<OrderItem>()
        };

        var newItem = new OrderItem
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Quantity = 1,
            ItemPrice = 6.99m
        };

        _daprClientMock
            .Setup(x => x.GetStateAsync<Order>(
                FastFoodConstants.StateStoreName,
                $"OrderProcessing-{orderId}",
                It.IsAny<ConsistencyMode?>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _service.AddItem(orderId, newItem));
        
        _daprClientMock.Verify(x => x.SaveStateAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Order>(),
            It.IsAny<StateOptions>(),
            It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
