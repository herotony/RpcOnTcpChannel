﻿#region License
/*
Copyright (c) 2013-2014 Daniil Rodin of Buhgalteria.Kontur team of SKB Kontur

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/
#endregion

using System;
using System.Threading.Tasks;
using SharpRpc.Codecs;
using SharpRpc.Interaction;
using SharpRpc.Logs;
using SharpRpc.ServerSide.Handler;

namespace SharpRpc.ServerSide
{
    public class IncomingRequestProcessor : IIncomingRequestProcessor
    {
        private readonly IServiceImplementationContainer serviceImplementationContainer;
        private readonly IHandlerContainer handlerContainer;
        private readonly IManualCodec<Exception> exceptionCodec;
        private readonly ILogger logger;

        public IncomingRequestProcessor(ILogger logger, IServiceImplementationContainer serviceImplementationContainer, 
            IHandlerContainer handlerContainer, ICodecContainer codecContainer)
        {
            this.logger = logger;
            this.serviceImplementationContainer = serviceImplementationContainer;
            this.handlerContainer = handlerContainer;
            exceptionCodec = codecContainer.GetManualCodecFor<Exception>();
        }      

        public async Task<Response> Process(Request request)
        {
            try
            {
                logger.IncomingRequest(request);
                var startTime = DateTime.Now;

                var implementationInfo = serviceImplementationContainer.GetImplementation(request.Path.ServiceName, request.ServiceScope);

                var methodHandler = handlerContainer.GetHandler(implementationInfo.Description, request.Path);

                var responseData = await methodHandler.Handle(implementationInfo.Implementation, request.Data, 0);

                var executionTime = DateTime.Now - startTime;
                logger.ProcessedRequestSuccessfully(request, executionTime);

                return new Response(ResponseStatus.Ok, responseData);
            }
            catch (ServiceNotReadyException)
            {
                logger.ProcessNotReady(request);
                return Response.NotReady;
            }
            catch (ServiceNotFoundException)
            {
                logger.ProcessedRequestWithBadStatus(request, ResponseStatus.ServiceNotFound);
                return Response.NotFound;
            }
            catch (InvalidPathException)
            {
                logger.ProcessedRequestWithBadStatus(request, ResponseStatus.BadRequest);
                return Response.BadRequest;
            }
            catch (InvalidImplementationException)
            {
                logger.ProcessedRequestWithBadStatus(request, ResponseStatus.InvalidImplementation);
                return Response.InvalidImplementation;
            }
            catch (Exception ex)
            {
                logger.ProcessedRequestWithException(request, ex);
                var responseData = exceptionCodec.EncodeSingle(ex);
                return new Response(ResponseStatus.Exception, responseData);
            }
        }
    }
}