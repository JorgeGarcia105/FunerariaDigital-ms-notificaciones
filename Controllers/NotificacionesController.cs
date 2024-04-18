using Microsoft.AspNetCore.Mvc;
using ms_notificaciones.Models;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SimpleEmail.Model;

using Amazon;
using System;
using System.Collections.Generic;
using Amazon.SimpleEmail;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Content = Amazon.SimpleEmail.Model.Content;
using DotNetEnv;
using System.Text.Json;
using System.ComponentModel.DataAnnotations; // Add this using directive

namespace ms_notificaciones.Controllers;

public interface INotification
{
}
public class ProductCreatedNotification : INotification
{
    public ProductCreatedNotification(int id, string? name, string? description)
    {
        Id = id;
        Name = name;
        Description = description;
    }

    public int Id { get; private set; }
    public string? Name { get; private set; }
    public string? Description { get; private set; }
}

public record CreateProductRequest(int Id, string? Name, string? Description);

[Route("[controller]")]
[ApiController]
public class ProductsController : ControllerBase
{
    
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly ILogger<ProductsController> _logger;
    private const string ProductTopic = "product-topic";

    public ProductsController(IAmazonSimpleNotificationService snsClient, ILogger<ProductsController> logger)
    {
        _snsClient = snsClient;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        //perform product request validation
        //save incoming product to database

        //create message
        var message = new ProductCreatedNotification(request.Id, request.Name, request.Description);

        //check if product topic already exists
        var topicArnExists = await _snsClient.FindTopicAsync(ProductTopic);

        //extract the topic arn of the sns topic
        //if the topic is not found, create a new topic
        string topicArn = "";
        if (topicArnExists == null)
        {
            var createTopicResponse = await _snsClient.CreateTopicAsync(ProductTopic);
            topicArn = createTopicResponse.TopicArn;
        }
        else
        {
            topicArn = topicArnExists.TopicArn;
        }
        //create and publish a new message to the sns topic arn
        var publishRequest = new PublishRequest()
        {
            TopicArn = topicArn,
            Message = JsonSerializer.Serialize(message),
            Subject = "ProductCreated"
        };
        _logger.LogInformation("Publish Request with the subject : '{subject}' and message: {message}", publishRequest.Subject, publishRequest.Message);
        await _snsClient.PublishAsync(publishRequest);
        return Ok();
    }
}

[ApiController]
[Route("[controller]")]
public class NotificacionesController : ControllerBase
{

    // Envio de sms
    [Route("enviar-sms")]
    [HttpPost]
    public async Task<ActionResult> EnviarSMSNuevaClave(ModeloSms datos) {

        var accesskey = Environment.GetEnvironmentVariable("ACCESS_KEY_AWS");
        var secretKey = Environment.GetEnvironmentVariable("SECRET_KEY_AWS");
        var client = new AmazonSimpleNotificationServiceClient(accesskey, secretKey, RegionEndpoint.USEast1);
        var messageAtributes = new Dictionary<string, MessageAttributeValue>();
        var smsType = new MessageAttributeValue {
            DataType = "String",
            StringValue = "Transactional"
        };

        messageAtributes.Add("AWS.SNS.SMS.SMSType", smsType);

        PublishRequest request = new PublishRequest {
            PhoneNumber = datos.numeroDestino,
            Message = datos.contenidoMensaje,
            MessageAttributes = messageAtributes
        };

        try {
            await client.PublishAsync(request);
            return Ok("SMS enviado correctamente");
        } catch{
            return BadRequest("Error al enviar el sms");
        }
    }
}
