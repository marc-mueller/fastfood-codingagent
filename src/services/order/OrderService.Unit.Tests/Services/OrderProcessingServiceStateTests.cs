using Dapr.Client;
using FastFood.Common;
using FinanceService.Observability;
using Microsoft.Extensions.Logging;
using Moq;
using OrderPlacement.Services;
using OrderService.Models.Entities;
using OrderService.Models.Helpers;
using OrderService.Unit.Tests.Helpers;

namespace OrderService.Unit.Tests.Services;

public class OrderProcessingServiceStateTests
{
    private readonly Mock<DaprClient> _daprClientMock;
    private readonly Mock<IOrderEventRouter> _orderEventRouterMock;
    private readonly Mock<ILogger<OrderProcessingServiceState>> _loggerMock;
    private readonly IOrderServiceObservability _observability;
    private readonly OrderProcessingServiceState _service;

    public OrderProcessingServiceStateTests()
    {
        _daprClientMock = new Mock<DaprClient>();
        _orderEventRouterMock = new Mock<IOrderEventRouter>();
        _loggerMock = new Mock<ILogger<OrderProcessingServiceState>>();
        _observability = new TestOrderServiceObservability();
        _service = new OrderProcessingServiceState(
            _daprClientMock.Object,
            _orderEventRouterMock.Object,
            _observability,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task AddItem_ShouldAddNewItem_WhenProductDoesNotExist()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            State = OrderState.Creating,
            Items = new List<OrderItem>()
        };

        var newItem = new OrderItem
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
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
        Assert.Equal(newItem.ProductId, order.Items.First().ProductId);
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
    public async Task AddItem_ShouldConsolidateQuantity_WhenSameProductExists()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        
        var existingItem = new OrderItem
        {
            Id = Guid.NewGuid(),
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
        Assert.Single(order.Items); // Should still have only one item
        Assert.Equal(productId, order.Items.First().ProductId);
        Assert.Equal(3, order.Items.First().Quantity); // 1 + 2 = 3
        Assert.Equal(existingItem.Id, order.Items.First().Id); // Should keep the existing item's ID
        
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
    public async Task AddItem_ShouldAddNewItem_WhenDifferentProductExists()
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
            ItemPrice = 4.99m,
            ProductDescription = "Fries"
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
        Assert.Equal(2, order.Items.Count); // Should have two items
        Assert.Contains(order.Items, i => i.ProductId == product1Id && i.Quantity == 1);
        Assert.Contains(order.Items, i => i.ProductId == product2Id && i.Quantity == 1);
        
        _daprClientMock.Verify(x => x.SaveStateAsync(
            FastFoodConstants.StateStoreName,
            $"OrderProcessing-{orderId}",
            order,
            It.IsAny<StateOptions>(),
            It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddItem_ShouldThrowException_WhenOrderIsNotInCreatingState()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            State = OrderState.Confirmed, // Not in Creating state
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
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AddItem(orderId, newItem)
        );
        
        Assert.Equal("Order is not in the correct state to add an item", exception.Message);
        
        // Verify that SaveStateAsync was not called
        _daprClientMock.Verify(x => x.SaveStateAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Order>(),
            It.IsAny<StateOptions>(),
            It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddItem_ShouldConsolidateMultipleTimes_WhenSameProductAddedRepeatedly()
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

        _daprClientMock
            .Setup(x => x.GetStateAsync<Order>(
                FastFoodConstants.StateStoreName,
                $"OrderProcessing-{orderId}",
                It.IsAny<ConsistencyMode?>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act - Add same product 3 times
        await _service.AddItem(orderId, new OrderItem
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Quantity = 1,
            ItemPrice = 6.99m
        });

        await _service.AddItem(orderId, new OrderItem
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Quantity = 2,
            ItemPrice = 6.99m
        });

        await _service.AddItem(orderId, new OrderItem
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Quantity = 3,
            ItemPrice = 6.99m
        });

        // Assert
        Assert.Single(order.Items); // Should still have only one item
        Assert.Equal(productId, order.Items.First().ProductId);
        Assert.Equal(6, order.Items.First().Quantity); // 1 + 2 + 3 = 6
    }
}
