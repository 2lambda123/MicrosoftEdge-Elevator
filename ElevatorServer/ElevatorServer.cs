﻿//--------------------------------------------------------------
//
// Microsoft Edge Elevator Server
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// MIT License
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files(the ""Software""),
// to deal in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and / or sell copies
// of the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions :
//
// The above copyright notice and this permission notice shall be included
// in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE AUTHORS
// OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF
// OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//--------------------------------------------------------------

using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace ElevatorServer
{
    internal class ElevatorServer : IDisposable
    {
        private StreamReader _inputPipeStream;
        private StreamWriter _outputPipeStream;
        private NamedPipeServerStream _pipeServer;

        public ElevatorServer()
        {
            // Need to specifically set the security to allow "Everyone" since this app runs as an admin
            // while the client runs as the default user
            PipeSecurity pSecure = new PipeSecurity();
            pSecure.SetAccessRule(new PipeAccessRule("Everyone", PipeAccessRights.ReadWrite, System.Security.AccessControl.AccessControlType.Allow));
            _pipeServer = new NamedPipeServerStream("TracingControllerPipe", PipeDirection.InOut, 10, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 255, 255, pSecure);
            _inputPipeStream = new StreamReader(_pipeServer);
            _outputPipeStream = new StreamWriter(_pipeServer);
        }

        public async Task ConnectAsync(CancellationToken cancelToken)
        {
            while (!cancelToken.IsCancellationRequested)
            {
                try
                {
                    await _pipeServer.WaitForConnectionAsync(cancelToken);
                }
                catch (OperationCanceledException)
                {
                    continue;
                }
            }
        }
        public string[] GetCommand()
        {
            // get a command from the client
            string line = null;

            // sometimes we receive a null or empty line from the client and need to skip
            while (string.IsNullOrEmpty(line))
            {
                line = _inputPipeStream.ReadLine();
            }

            // A command line from the client is delimited by spaces
            var messageTokens = line.Split(' ');

            // the first token of the command line is the actual command
            string command = messageTokens[0];

            switch (command)
            {
                case "PASS_START":
                case "START_BROWSER":
                case "END_BROWSER":
                case "PASS_END":
                    break;
                default:
                    throw new Exception($"Unknown command encountered: {command}");
            } // switch (Command)

            return messageTokens;
        }

        public void AcknowledgeCommand()
        {
            // acknowledge
            _outputPipeStream.WriteLine("OK");
            _outputPipeStream.Flush();
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool isDisposing)
        {
            if (_pipeServer != null)
            {
                _pipeServer.Dispose();
                _pipeServer = null;
            }

            GC.SuppressFinalize(this);
        }

        internal void Shutdown()
        {
            Dispose();
        }
    }
}