#region -- License Terms --
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
using System.Threading;

namespace MsgPack.Rpc
{
	// TODO: Move to NLiblet
	/// <summary>
	///		The basic implementation of the <see cref="ILeaseable{T}"/>.
	/// </summary>
	public abstract class Leaseable<T> : ILeaseable<Leaseable<T>>, IDisposable
		where T : class
	{
		private ILease<Leaseable<T>> _lease;

		// Test purposes only.
		internal bool IsLeased
		{
			get { return this._lease != null; }
		}

		void ILeaseable<Leaseable<T>>.SetLease( ILease<Leaseable<T>> lease )
		{
			this.SetLease( lease );
		}

		/// <summary>
		///		Sets the <see cref="ILease{T}"/> to handle graceful returning.
		/// </summary>
		/// <param name="lease">
		///		The <see cref="ILease{T}"/> to handle graceful returning.
		///		The pool will pass <c>null</c> for this parameter when the object is returned to pool.
		/// </param>
		protected void SetLease( ILease<Leaseable<T>> lease )
		{
			if ( Interlocked.CompareExchange( ref this._lease, lease, null ) != null )
			{
				throw new InvalidOperationException( "Already leased." );
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Leaseable"/> class.
		/// </summary>
		protected Leaseable() { }

		/// <summary>
		/// Releases unmanaged resources and performs other cleanup operations before the
		/// <see cref="Leaseable"/> is reclaimed by garbage collection.
		/// </summary>
		~Leaseable()
		{
			this.Dispose( false );
		}

		/// <summary>
		///		Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose()
		{
			this.Dispose( true );
			GC.SuppressFinalize( this );
		}

		/// <summary>
		/// Releases unmanaged and - optionally - managed resources
		/// </summary>
		/// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
		protected virtual void Dispose( bool disposing )
		{
			var lease = Interlocked.Exchange( ref this._lease, null );
			if ( lease != null && disposing )
			{
				lease.Dispose();
			}
		}
	}
}