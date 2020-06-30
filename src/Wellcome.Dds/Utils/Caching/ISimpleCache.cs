using System;

namespace Digirati.Util.Caching
{
    /// <summary>
    /// This interface encapsulates what we usually want to do with a cache in simple terms.
    /// It can act as an abstraction over a "real" cache (like the ASP.NET runtime cache).
    /// </summary>
    /// <para>See "The Design of a large Mediasurface Web Application.docx" for a full
    /// discussion of the concepts behind the classes in this Caching namespace.</para>
    /// <para>
    /// In many scenarios, use of a cache goes like this:
    /// 
    /// MyObject myObject = (get myObject from cache with key xxx)
    /// if(myObject == null)
    /// {
    ///     (make new myObject) // this might take some time
    ///     (put new myObject in cache under key xxx with timeout y)
    /// }
    /// (do something with myobject)
    /// 
    /// What ISimpleCache does turn this upside down. It supplies the instructions to create a new myObject when it
    /// asks for something from the cache:
    /// 
    /// ISimpleCache simpleCache...; 
    /// myObject = simpleCache.GetCached(y, xxx, (make new myObject))
    /// 
    /// </para>
    /// <para>
    /// It supplies the cache with the code it needs to create a new instance of the thing
    /// being cached - as the delegate createObject.
    /// If the cache is empty <see cref="NonCachingCache"/> then the ISimpleCache implementation
    /// will run the code (make new myObject) there and then, to return the object.
    /// </para>
    /// <para>
    /// The crucial point is that the client doesn't invoke the createObject delegate - the cache
    /// does. And the cache could invoke this method independently, later on - most usefully, if it detects
    /// that the object being cached is going to expire soon, it could invoke the createObject delegate
    /// to create a fresh one in advance - which means that callers would never have to bear the cost
    /// of a stale cache hit.
    /// </para>
    /// <para>
    /// Another drawback of the initial (regular) caching scenario is there's nothing stopping
    /// the (make new object) code being run multiple times in parallel, if another thread enters this
    /// code section while (make new object) is still running. If (make new object) takes a long time,
    /// and we're in a very high traffic web site, then this is a likely scenario and can lead to 
    /// catastrophic race conditions.
    /// ISimpleCache implementations can deal with this, if they choose.
    /// </para>
    public interface ISimpleCache
    {
        /// <summary>
        /// Get the object that this cache is storing. This might be a simple object, or it might be
        /// a collection like a List or Dictionary.
        /// </summary>
        /// <typeparam name="TResult">the type of thing we want to cache</typeparam>
        /// <param name="maxAgeSeconds">how old we’re prepared to accept it</param>
        /// <param name="cacheKey">the key that our cache will store its object under in the underlying "real" cache</param>
        /// <param name="createObject">a delegate that the cache can call to rebuild the 
        /// cached object if it’s stale - i.e., the code to run to make a new one.</param>
        /// <returns></returns>
        TResult GetCached<TResult>(
            int maxAgeSeconds,
            string cacheKey,
            Func<TResult> createObject)
            where TResult : class;

        /// <summary>
        /// Remove "key" from the cache.
        /// </summary>
        /// <param name="key"></param>
        void Remove(string key);
    }

}
