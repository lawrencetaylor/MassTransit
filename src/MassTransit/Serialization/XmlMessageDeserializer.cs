// Copyright 2007-2015 Chris Patterson, Dru Sellers, Travis Smith, et. al.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace MassTransit.Serialization
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Net.Mime;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml;
    using System.Xml.Linq;
    using Newtonsoft.Json;


    public class XmlMessageDeserializer :
        IMessageDeserializer
    {
        readonly JsonSerializer _deserializer;
        readonly IPublishSendEndpointProvider _publishEndpoint;
        readonly ISendEndpointProvider _sendEndpointProvider;

        public XmlMessageDeserializer(JsonSerializer deserializer, ISendEndpointProvider sendEndpointProvider,
            IPublishSendEndpointProvider publishEndpoint)
        {
            _deserializer = deserializer;
            _sendEndpointProvider = sendEndpointProvider;
            _publishEndpoint = publishEndpoint;
        }

        async void IProbeSite.Probe(ProbeContext context)
        {
            ProbeContext scope = context.CreateScope("xml");
            scope.Add("contentType", XmlMessageSerializer.XmlContentType.MediaType);
        }

        ContentType IMessageDeserializer.ContentType
        {
            get { return XmlMessageSerializer.XmlContentType; }
        }

        ConsumeContext IMessageDeserializer.Deserialize(ReceiveContext receiveContext)
        {
            try
            {
                XDocument document;
                using (Stream body = receiveContext.GetBody())
                using (var xmlReader = new XmlTextReader(body))
                    document = XDocument.Load(xmlReader);

                var json = new StringBuilder(1024);

                using (var stringWriter = new StringWriter(json, CultureInfo.InvariantCulture))
                using (var jsonWriter = new JsonTextWriter(stringWriter))
                {
                    jsonWriter.Formatting = Newtonsoft.Json.Formatting.None;

                    XmlMessageSerializer.XmlSerializer.Serialize(jsonWriter, document.Root);
                }

                MessageEnvelope envelope;
                using (var stringReader = new StringReader(json.ToString()))
                using (var jsonReader = new JsonTextReader(stringReader))
                {
                    envelope = _deserializer.Deserialize<MessageEnvelope>(jsonReader);
                }

                return new JsonConsumeContext(_deserializer, _sendEndpointProvider, _publishEndpoint, receiveContext, envelope);
            }
            catch (JsonSerializationException ex)
            {
                throw new SerializationException("A JSON serialization exception occurred while deserializing the message envelope", ex);
            }
            catch (SerializationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new SerializationException("An exception occurred while deserializing the message envelope", ex);
            }
        }
    }
}