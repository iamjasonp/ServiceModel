using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using System;
using System.ServiceModel.Channels;

namespace ServiceModel
{
    internal abstract class Class1
    {
 
        protected readonly Guid _identity = Guid.NewGuid();
        private readonly Socket _socket;
        protected readonly BufferManager BufferManager;
        protected readonly AutoResetEvent SendEvent = new AutoResetEvent(false);
        protected volatile bool Disposed;
        private volatile bool _isDead;
        private volatile bool _inUse = false;
        private Timer _timer;
        private int _timeInterval;
        private int _closeAttempts;

        protected Class1(Socket socket, BufferManager bufferManager, IPEndPoint endPoint)
        {
            _socket = socket;
            BufferManager = bufferManager;
            EndPoint = endPoint;
        }

        /// <summary>
        /// The Socket used for IO.
        /// </summary>
        public Socket Socket
        {
            get { return _socket; }
        }

        /// <summary>
        /// Unique identifier for this connection.
        /// </summary>
        public Guid Identity
        {
            get { return _identity; }
        }

        /// <summary>
        /// True if the connection has been SASL authenticated.
        /// </summary>
        public bool IsAuthenticated { get; set; }

        /// <summary>
        /// Gets or sets the write buffer.
        /// </summary>
        /// <value>
        /// The write buffer for building the request packet.
        /// </value>
        public byte[] WriteBuffer { get; set; }

        /// <summary>
        /// True if connection is using SSL
        /// </summary>
        public bool IsSecure { get; protected set; }

        /// <summary>
        /// Gets the remote hosts <see cref="EndPoint"/> that this <see cref="Connection"/> is connected to.
        /// </summary>
        /// <value>
        /// The end point.
        /// </value>
        public EndPoint EndPoint { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is dead.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is dead; otherwise, <c>false</c>.
        /// </value>
        public bool IsDead
        {
            get { return _isDead; }
            set { _isDead = value; }
        }

        /// <summary>
        /// Gets or sets the maximum times that the client will check the <see cref="InUse"/>
        /// property before closing the connection.
        /// </summary>
        /// <value>
        /// The maximum close attempts.
        /// </value>
        public int MaxCloseAttempts { get; set; }

        /// <summary>
        ///  Checks whether this <see cref="Connection"/> is currently being used to execute a request.
        /// </summary>
        /// <value>
        ///   <c>true</c> if if this <see cref="Connection"/> is in use; otherwise, <c>false</c>.
        /// </value>
        public bool InUse { get { return _inUse; } }

        /// <summary>
        /// Gets the number of close attempts that this <see cref="Connection"/> has attemped.
        /// </summary>
        /// <value>
        /// The close attempts.
        /// </value>
        public int CloseAttempts { get { return _closeAttempts; } }

        /// <summary>
        /// Gets a value indicating whether this instance is shutting down.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance has shutdown; otherwise, <c>false</c>.
        /// </value>
        public bool HasShutdown { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this instance is disposed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is disposed; otherwise, <c>false</c>.
        /// </value>
        public bool IsDisposed { get { return Disposed; } }

        public virtual byte[] Send(byte[] request)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Marks this <see cref="Connection"/> as used; meaning it cannot be disposed unless <see cref="InUse"/>
        /// is <c>false</c> or the <see cref="MaxCloseAttempts"/> has been reached.
        /// </summary>
        /// <param name="isUsed">if set to <c>true</c> [is used].</param>
        public void MarkUsed(bool isUsed)
        {
            _inUse = isUsed;
        }

        /// <summary>
        /// Disposes this <see cref="Connection"/> if <see cref="InUse"/> is <c>false</c>; otherwise
        /// it will wait for the interval and attempt again up until the <see cref="MaxCloseAttempts"/>
        /// threshold is met or <see cref="InUse"/> is <c>false</c>.
        /// </summary>
        /// <param name="interval">The interval to wait between close attempts.</param>
        public void CountdownToClose(int interval)
        {
            _timeInterval = interval;
            HasShutdown = true;
            _timer = new Timer(_timer_callback, null, interval, Timeout.Infinite);
        }

        private void _timer_callback(Object state)
        {
            _closeAttempts = Interlocked.Increment(ref _closeAttempts);
            if (InUse && _closeAttempts < MaxCloseAttempts && !IsDead)
            {
                _timer.Change(_timeInterval, Timeout.Infinite);
            }
            else
            {
                //mark dead
                IsDead = true;

                //this will call the derived classes Dispose method,
                //which call the base.Dispose (on OperationBase) cleaning up the timer.
                Dispose();
            }
        }

        /// <summary>
        /// Disposes the <see cref="Timer"/> used for checking whether or not the connection
        /// is in use and can be Disposed; <see cref="InUse"/> will be set to <c>false</c>.
        /// </summary>
        public virtual void Dispose()
        {
            if (_timer == null) return;
            _inUse = false;
            _timer.Dispose();
        }
    }
}