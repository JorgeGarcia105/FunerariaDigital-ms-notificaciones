using Microsoft.AspNetCore.Mvc;
using SendGrid;
using SendGrid.Helpers.Mail;
using ms_notificaciones.Models;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon;
using System;
using System.Collections.Generic;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Content = Amazon.SimpleEmail.Model.Content;
using DotNetEnv;
using System.Text.Json; // Add this using directive

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
    /** Método para enviar un correo de bienvenida a un usuario a traves de 
     * @param datos Modelo de datos del correo
     * @return Resultado de la operación
     */
    [Route("enviar-correo")]
    [HttpPost]
    public async Task<IActionResult> EnviarCorreoBienvenida(ModeloCorreo datos) {
        try {
            // Obtener las credenciales de AWS SES desde variables de entorno
            var accesskey = Environment.GetEnvironmentVariable("ACCESS_KEY_AWS_GMAIL");
            var secretKey = Environment.GetEnvironmentVariable("SECRET_KEY_AWS_GMAIL");

            // Crear cliente de Amazon SES
            var client = new AmazonSimpleEmailServiceClient(accesskey, secretKey, RegionEndpoint.USEast1);

            // Crear y enviar la solicitud de correo electrónico

            SendEmailRequest sendRequest = this.CreateBaseMessage(datos);

             // Leer la plantilla de correo electrónico
            var emailTemplate = System.IO.File.ReadAllText("./plantillas/index.html");

            // Reemplazar los marcadores de posición con los datos reales
            emailTemplate = emailTemplate.Replace("{FirstName}", datos.nombreDestino);
            emailTemplate = emailTemplate.Replace("{2FACode}", datos.contenidoCorreo);


            // Enviar el correo electrónico
            sendRequest.Message = new Message
            {
                Subject = new Content(datos.asuntoCorreo),
                Body = new Body
                {
                    Html = new Content
                    {
                        Charset = "UTF-8",
                        Data = emailTemplate
                    },
                    Text = new Content
                    {
                        Charset = "UTF-8",
                        Data = emailTemplate
                    }
                }
            };
            var response = await client.SendEmailAsync(sendRequest);
            // Verificar si el correo electrónico se envió correctamente
            if(response.HttpStatusCode == System.Net.HttpStatusCode.OK) {
                return Ok("Correo enviado correctamente");
            } else {
                // Loggear cualquier error
                Console.WriteLine($"Error al enviar el correo a {datos.correoDestino}. Estado HTTP: {response.HttpStatusCode}");
                return BadRequest("Error al enviar el correo");
            }
        } catch(Exception ex) {
            // Loggear cualquier excepción y devolver un error
            Console.WriteLine($"Error al enviar el correo: {ex.Message}");
            return BadRequest("Error al enviar el correo" +  ex.Message);
        }
    }     

    /** Método para crear la solicitud de correo electrónico
     * @param datos Modelo de datos del correo
     * @return Solicitud de correo electrónico
     */
    private SendEmailRequest CreateBaseMessage(ModeloCorreo datos) {
        // Obtener información del remitente y destinatario del modelo
        var from = new EmailAddress(Environment.GetEnvironmentVariable("EMAIL_FROM"), Environment.GetEnvironmentVariable("NAME_FROM"));
        var subject = datos.asuntoCorreo;
        Console.WriteLine("Correo de destino: " + datos.correoDestino);
        Console.WriteLine("Nombre de destino: " + datos.nombreDestino);
        var to = new EmailAddress(datos.correoDestino, datos.nombreDestino);
        var plainTextContent = datos.contenidoCorreo;
        var htmlContent = datos.contenidoCorreo;

        // Crear la solicitud de correo electrónico
        return new SendEmailRequest {
            Source = from.Email,
            Destination = new Destination {
                ToAddresses = new List<string> { to.Email }
            },
            Message = new Message {
                Subject = new Content(subject),
                Body = new Body {
                    Html = new Content(htmlContent),
                    Text = new Content(plainTextContent)
                }
            }
        };
    }

    // Envio de sms
    [Route("enviar-sms")]
    [HttpPost]
public async Task<ActionResult> EnviarSMSNuevaClave(ModeloSms datos) {
    var accessKey = Environment.GetEnvironmentVariable("ACCESS_KEY_AWS");
    var secretKey = Environment.GetEnvironmentVariable("SECRET_KEY_AWS");
    var client = new AmazonSimpleNotificationServiceClient(accessKey, secretKey, RegionEndpoint.USEast1);

    // Leer la plantilla desde el archivo
    var smsTemplate = System.IO.File.ReadAllText("./plantillas/nuevaclave.txt");

    // Reemplazar los marcadores de posición con los datos reales
    var mensaje = smsTemplate.Replace("{Nombre}", datos.nombreDestinatario)
                             .Replace("{Clave}", datos.contenidoMensaje);

    // Configurar y enviar el SMS
    var messageAttributes = new Dictionary<string, MessageAttributeValue> {
        {"AWS.SNS.SMS.SMSType", new MessageAttributeValue { DataType = "String", StringValue = "Transactional" }}
    };

    var request = new PublishRequest {
        PhoneNumber = datos.numeroDestino,
        Message = mensaje,
        MessageAttributes = messageAttributes
    };

    try {
        await client.PublishAsync(request);
        return Ok("SMS enviado correctamente");
    } catch (Exception ex) {
        return BadRequest("Error al enviar el sms: " + ex.Message);
    }
}
}
