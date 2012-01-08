﻿#region -- License Terms --
//
// MessagePack for CLI
//
// Copyright (C) 2010 FUJIWARA, Yusuke
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//
#endregion -- License Terms --

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NUnit.Framework;

namespace MsgPack.Rpc.Server.Protocols
{
	// TODOs
	// Error test
	// Refactor move deserialization from transport to session.

	// Client:
	//   IDL

	// Others
	//   UDP
	//   Client SocketPool
	//   
	[TestFixture]
	public class TcpServerTransportTest
	{
		// TODO: Error packet test.
		private const bool _traceEnabled = true;
		private DebugTraceSourceSetting _debugTrace;

		[SetUp]
		public void SetUp()
		{
			this._debugTrace = new DebugTraceSourceSetting( _traceEnabled , MsgPackRpcServerTrace.Source, MsgPackRpcServerProtocolsTrace.Source );
		}

		[TearDown]
		public void TearDown()
		{
			this._debugTrace.Dispose();
		}

		[Test]
		public void TestEchoRequest()
		{
			try
			{
				bool isOk = false;
				string message = "Hello, world";
				using ( var waitHandle = new ManualResetEventSlim() )
				{
					using ( var server =
						CallbackServer.Create(
							( id, args ) =>
							{
								try
								{
									Assert.That( args[ 0 ] == message, args[ 0 ].ToString() );
									Assert.That( args[ 1 ].IsTypeOf<Int64>().GetValueOrDefault(), args[ 1 ].ToString() );
									isOk = true;
									return args;
								}
								finally
								{
									waitHandle.Set();
								}
							}
						)
					)
					using ( var manager = new TcpServerTransportManager( server ) )
					{

						TestEchoRequestCore( ref isOk, waitHandle, message );

						waitHandle.Reset();
						isOk = false;
						// Again
						TestEchoRequestCore( ref isOk, waitHandle, message );

						waitHandle.Reset();
						isOk = false;
						// Again 2
						TestEchoRequestCore( ref isOk, waitHandle, message );
					}
				}
			}
			catch ( SocketException sockEx )
			{
				Console.Error.WriteLine( "{0}({1}:0x{1:x8})", sockEx.SocketErrorCode, sockEx.ErrorCode );
				Console.Error.WriteLine( sockEx );
				throw;
			}
		}

		private static void TestEchoRequestCore( ref bool isOk, ManualResetEventSlim waitHandle, string message )
		{
			using ( var client = new TcpClient() )
			{
				client.Connect( new IPEndPoint( IPAddress.Loopback, CallbackServer.PortNumber ) );

				var now = MessagePackConvert.FromDateTime( DateTime.Now );

				using ( var stream = client.GetStream() )
				using ( var packer = Packer.Create( stream ) )
				{
					Console.WriteLine( "---- Client sending request ----" );

					packer.PackArrayHeader( 4 );
					packer.Pack( 0 );
					packer.Pack( 123 );
					packer.Pack( "Echo" );
					packer.PackArrayHeader( 2 );
					packer.Pack( message );
					packer.Pack( now );

					Console.WriteLine( "---- Client sent request ----" );

					if ( Debugger.IsAttached )
					{
						waitHandle.Wait();
					}
					else
					{
						Assert.That( waitHandle.Wait( TimeSpan.FromSeconds( 3 ) ) );
					}

					Console.WriteLine( "---- Client receiving response ----" );
					var result = Unpacking.UnpackObject( stream );
					Assert.That( result.IsArray );
					var array = result.AsList();
					Assert.That( array.Count, Is.EqualTo( 4 ) );
					Assert.That( array[ 0 ] == 1, array[ 0 ].ToString() );
					Assert.That( array[ 1 ] == 123, array[ 1 ].ToString() );
					Assert.That( array[ 2 ] == MessagePackObject.Nil, array[ 2 ].ToString() );
					Assert.That( array[ 3 ].IsArray, array[ 3 ].ToString() );
					var returnValue = array[ 3 ].AsList();
					Assert.That( returnValue.Count, Is.EqualTo( 2 ) );
					Assert.That( returnValue[ 0 ] == message, returnValue[ 0 ].ToString() );
					Assert.That( returnValue[ 1 ] == now, returnValue[ 1 ].ToString() );
					Console.WriteLine( "---- Client received response ----" );
				}
			}
		}

		[Test]
		public void TestEchoRequestContinuous()
		{
			try
			{
				const int count = 3;
				bool[] serverStatus = new bool[ count ];
				string message = "Hello, world";
				using ( var waitHandle = new CountdownEvent( count ) )
				using ( var server =
					CallbackServer.Create(
						( id, args ) =>
						{
							try
							{
								Assert.That( args[ 0 ] == message );
								Assert.That( args[ 1 ].IsTypeOf<Int64>().GetValueOrDefault() );
								lock ( serverStatus )
								{
									serverStatus[ id.Value ] = true;
								}

								return args;
							}
							finally
							{
								waitHandle.Signal();
							}
						}
					)
				)
				using ( var manager = new TcpServerTransportManager( server ) )
				{
					TestEchoRequestContinuousCore( serverStatus, waitHandle, count, message );

					waitHandle.Reset();
					Array.Clear( serverStatus, 0, serverStatus.Length );
					// Again
					TestEchoRequestContinuousCore( serverStatus, waitHandle, count, message );
				}
			}
			catch ( SocketException sockEx )
			{
				Console.Error.WriteLine( "{0}({1}:0x{1:x8})", sockEx.SocketErrorCode, sockEx.ErrorCode );
				Console.Error.WriteLine( sockEx );
				throw;
			}
		}

		private static void TestEchoRequestContinuousCore( bool[] serverStatus, CountdownEvent waitHandle, int count, string message )
		{
			using ( var client = new TcpClient() )
			{
				client.Connect( new IPEndPoint( IPAddress.Loopback, CallbackServer.PortNumber ) );

				var now = MessagePackConvert.FromDateTime( DateTime.Now );
				bool[] resposeStatus = new bool[ count ];

				using ( var stream = client.GetStream() )
				using ( var packer = Packer.Create( stream ) )
				{
					for ( int i = 0; i < count; i++ )
					{
						Console.WriteLine( "---- Client sending request ----" );
						packer.PackArrayHeader( 4 );
						packer.Pack( 0 );
						packer.Pack( i );
						packer.Pack( "Echo" );
						packer.PackArrayHeader( 2 );
						packer.Pack( message );
						packer.Pack( now );
						Console.WriteLine( "---- Client sent request ----" );
					}

					if ( Debugger.IsAttached )
					{
						waitHandle.Wait();
					}
					else
					{
						Assert.That( waitHandle.Wait( TimeSpan.FromSeconds( count * 3 ) ) );
					}

					for ( int i = 0; i < count; i++ )
					{
						Console.WriteLine( "---- Client receiving response ----" );
						var array = Unpacking.UnpackArray( stream );
						Assert.That( array.Count, Is.EqualTo( 4 ) );
						Assert.That( array[ 0 ] == 1, array[ 0 ].ToString() );
						Assert.That( array[ 1 ].IsTypeOf<int>().GetValueOrDefault() );
						resposeStatus[ array[ 1 ].AsInt32() ] = true;
						Assert.That( array[ 2 ] == MessagePackObject.Nil, array[ 2 ].ToString() );
						Assert.That( array[ 3 ].IsArray, array[ 3 ].ToString() );
						var returnValue = array[ 3 ].AsList();
						Assert.That( returnValue.Count, Is.EqualTo( 2 ) );
						Assert.That( returnValue[ 0 ] == message, returnValue[ 0 ].ToString() );
						Assert.That( returnValue[ 1 ] == now, returnValue[ 1 ].ToString() );
						Console.WriteLine( "---- Client received response ----" );
					}

					lock ( serverStatus )
					{
						Assert.That( serverStatus, Is.All.True, String.Join( ", ", serverStatus ) );
					}
					Assert.That( resposeStatus, Is.All.True );
				}
			}
		}
	}
}
