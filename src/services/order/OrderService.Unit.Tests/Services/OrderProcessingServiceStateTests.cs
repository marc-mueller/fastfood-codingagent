using Dapr.Client;
using FastFood.Common;
using Moq;
using OrderPlacement.Services;
using OrderService.Models.Entities;
using OrderService.Models.Helpers;
using FinanceService.Observability;
using Microsoft.Extensions.Logging;
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
    public async Task AddItem_WhenItemDoesNotExist_AddsNewItem()
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
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<ConsistencyMode?>(), 
                It.IsAny<IReadOnlyDictionary<string, string>?>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        await _service.AddItem(orderId, newItem);

        // Assert
        Assert.Single(order.Items);
        Assert.Equal(productId, order.Items.First().ProductId);
        Assert.Equal(2, order.Items.First().Quantity);
        
        _daprClientMock.Verify(x => x.SaveStateAsync(
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<Order>(), 
            It.IsAny<StateOptions?>(), 
            It.IsAny<IReadOnlyDictionary<string, string>?>(), 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddItem_WhenItemExists_MergesQuantity()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var existingItemId = Guid.NewGuid();
        
        var order = new Order
        {
            Id = orderId,
            State = OrderState.Creating,
            Items = new List<OrderItem>
            {
                new OrderItem
                {
                    Id = existingItemId,
                    ProductId = productId,
                    Quantity = 1,
                    ItemPrice = 6.99m,
                    ProductDescription = "Cheeseburger"
                }
            }
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
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<ConsistencyMode?>(), 
                It.IsAny<IReadOnlyDictionary<string, string>?>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        await _service.AddItem(orderId, newItem);

        // Assert
        Assert.Single(order.Items);
        Assert.Equal(productId, order.Items.First().ProductId);
        Assert.Equal(3, order.Items.First().Quantity); // 1 + 2 = 3
        Assert.Equal(existingItemId, order.Items.First().Id); // Should keep the existing item's ID
        
        _daprClientMock.Verify(x => x.SaveStateAsync(
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<Order>(), 
            It.IsAny<StateOptions?>(), 
            It.IsAny<IReadOnlyDictionary<string, string>?>(), 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddItem_WhenMultipleDifferentItemsExist_AddsNewItem()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var productId1 = Guid.NewGuid();
        var productId2 = Guid.NewGuid();
        
        var order = new Order
        {
            Id = orderId,
            State = OrderState.Creating,
            Items = new List<OrderItem>
            {
                new OrderItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId1,
                    Quantity = 1,
                    ItemPrice = 6.99m,
                    ProductDescription = "Cheeseburger"
                }
            }
        };

        var newItem = new OrderItem
        {
            Id = Guid.NewGuid(),
            ProductId = productId2,
            Quantity = 1,
            ItemPrice = 4.99m,
            ProductDescription = "Fries"
        };

        _daprClientMock
            .Setup(x => x.GetStateAsync<Order>(
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<ConsistencyMode?>(), 
                It.IsAny<IReadOnlyDictionary<string, string>?>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        await _service.AddItem(orderId, newItem);

        // Assert
        Assert.Equal(2, order.Items.Count);
        Assert.Contains(order.Items, i => i.ProductId == productId1 && i.Quantity == 1);
        Assert.Contains(order.Items, i => i.ProductId == productId2 && i.Quantity == 1);
        
        _daprClientMock.Verify(x => x.SaveStateAsync(
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<Order>(), 
            It.IsAny<StateOptions?>(), 
            It.IsAny<IReadOnlyDictionary<string, string>?>(), 
            It.IsAny<CancellationToken>()), Times.Once);
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
            ItemPrice = 6.99m,
            ProductDescription = "Cheeseburger"
        };

        _daprClientMock
            .Setup(x => x.GetStateAsync<Order>(
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<ConsistencyMode?>(), 
                It.IsAny<IReadOnlyDictionary<string, string>?>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.AddItem(orderId, newItem));
        
        _daprClientMock.Verify(x => x.SaveStateAsync(
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<Order>(), 
            It.IsAny<StateOptions?>(), 
            It.IsAny<IReadOnlyDictionary<string, string>?>(), 
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
