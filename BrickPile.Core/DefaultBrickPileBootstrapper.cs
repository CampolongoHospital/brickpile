﻿using System;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Http.Dispatcher;
using System.Web.Mvc;
using System.Web.Routing;
using BrickPile.Core.Conventions;
using BrickPile.Core.Extensions;
using BrickPile.Core.Graph;
using BrickPile.Core.Infrastructure.Listeners;
using BrickPile.Core.Mvc;
using BrickPile.Core.Routing;
using BrickPile.Core.Routing.Trie;
using Raven.Abstractions.Extensions;
using Raven.Client;
using Raven.Client.Embedded;
using Raven.Client.Indexes;
using Raven.Json.Linq;
using StructureMap;
using StructureMap.Graph;

namespace BrickPile.Core
{
    /// <summary>
    ///     Responsible for handling the default initialisation of BrickPile
    /// </summary>
    public class DefaultBrickPileBootstrapper : IBrickPileBootstrapper
    {
        private const string ConnectionStringName = "RavenDB";
        private const string DataDirectory = "~/App_Data/Raven";
        public const string TrieId = "brickpile/trie";

        private readonly BrickPileConventions conventions;

        /// <summary>
        ///     Gets the document store.
        /// </summary>
        /// <value>
        ///     The document store.
        /// </value>
        protected IDocumentStore DocumentStore { get; private set; }

        /// <summary>
        ///     Gets or sets the application container.
        /// </summary>
        /// <value>
        ///     The application container.
        /// </value>
        protected IContainer ApplicationContainer { get; set; }

        /// <summary>
        ///     Gets the conventions.
        /// </summary>
        /// <value>
        ///     The conventions.
        /// </value>
        protected virtual BrickPileConventions Conventions
        {
            get { return this.conventions; }
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="DefaultBrickPileBootstrapper" /> class.
        /// </summary>
        protected DefaultBrickPileBootstrapper()
        {
            this.conventions = new BrickPileConventions();
        }

        /// <summary>
        ///     Initialises BrickPile.
        /// </summary>
        public void Initialise()
        {
            this.DocumentStore = this.InitialiseDocumentStore();

            this.ApplicationContainer = this.GetApplicationContainer();

            this.ConfigureApplicationContainerInternal(this.ApplicationContainer, this.DocumentStore);

            this.ConfigureApplicationContainer(this.ApplicationContainer);

            this.ConfigureConventions(this.Conventions);

            this.CreateDefaultDocuments(this.DocumentStore);

            this.RegisterCustomRoutes(RouteTable.Routes);

            // Register structuremap as dependency resolver
            DependencyResolver.SetResolver(new StructureMapDependencyResolver(this.ApplicationContainer));

            // Set the dependency resolver for the web api
            GlobalConfiguration.Configuration.Services.Replace(typeof (IHttpControllerActivator),
                new StructureMapControllerActivator(this.ApplicationContainer));

            // Extended metadata provider handling GroupName on the DisplayAttribute
            ModelMetadataProviders.Current = new ExtendedDataAnnotationsModelMetadataProvider();

            ModelValidatorProviders.Providers.Add(new ContentTypeMetadataValidatorProvider());

            ModelMetadataProviders.Current = new MetadataProvider();

            ControllerBuilder.Current.SetControllerFactory(typeof (BrickPileControllerFactory));

            RouteTable.Routes.RouteExistingFiles = false;

            RouteTable.Routes.AppendTrailingSlash = true;

            RouteTable.Routes.LowercaseUrls = true;

            ViewEngines.Engines.Clear();

            ViewEngines.Engines.Add(new RazorViewEngine());

            // Ensure secure by default
            GlobalFilters.Filters.Add(new AuthorizeContentAttribute(this.DocumentStore));

            // Add editor tools as global filter
            GlobalFilters.Filters.Add(new EditorControlsAttribute());
        }


        /// <summary>
        ///     Gets the Container instance - automatically set during initialise.
        /// </summary>
        /// <returns></returns>
        protected IContainer GetApplicationContainer()
        {
            return ObjectFactory.Container;
        }

        /// <summary>
        ///     Setups the default documents.
        /// </summary>
        /// <param name="documentStore">The document store.</param>
        protected void CreateDefaultDocuments(IDocumentStore documentStore)
        {
            using (var session = this.DocumentStore.OpenSession())
            {
                var trie = session.Load<Trie>(TrieId);

                if (trie != null) return;
                trie = new Trie { Id = TrieId };
                session.Store(trie);
                session.SaveChanges();
            }
        }

        [Obsolete("not used atm", false)]
        internal void OnPageSave(string key, IPage currentPage, RavenJObject metadata)
        {
            using (var session = this.DocumentStore.OpenSession())
            {
                var trie = session.Load<Trie>(TrieId);

                if (trie.RootNode == null)
                {
                    trie.RootNode = new TrieNode
                    {
                        PageId = key
                    };
                }
                else
                {
                    var nodes = trie.RootNode.Flatten(n => n.Children).ToArray();

                    var parent = currentPage.Parent != null
                        ? nodes.SingleOrDefault(
                            n =>
                                String.Equals(n.PageId, currentPage.Parent.Id, StringComparison.CurrentCultureIgnoreCase))
                        : null;

                    if (parent != null)
                    {
                        currentPage.Metadata.Slug = Slug.CreateSlug(currentPage);
                        currentPage.Metadata.Url = currentPage.Metadata.Slug.Insert(0,
                            VirtualPathUtility.AppendTrailingSlash(parent.Url ?? ""));

                        if (parent.Children.All(n => n.PageId != key.Replace("/draft", "")))
                        {
                            parent.Children.Add(new TrieNode
                            {
                                PageId = key.Replace("/draft", ""),
                                ParentId = parent.PageId,
                                Url = currentPage.Metadata.Url
                            });
                        }
                    }
                }

                session.SaveChanges();
            }
        }

        /// <summary>
        ///     Called when [page publish].
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="currentPage">The current page.</param>
        /// <param name="metadata">The metadata.</param>
        internal void OnPagePublish(string key, IPage currentPage, RavenJObject metadata)
        {
            using (var session = this.DocumentStore.OpenSession())
            {
                var trie = session.Load<Trie>(TrieId);

                if (trie.RootNode == null)
                {
                    trie.RootNode = new TrieNode
                    {
                        PageId = key.Replace("/draft", "")
                    };
                }
                else
                {
                    var nodes = trie.RootNode.Flatten(n => n.Children).ToArray();

                    var parentNode = currentPage.Parent != null
                        ? nodes.SingleOrDefault(n => n.PageId.CompareToIgnoreDraftId(currentPage.Parent.Id))
                        : null;

                    var currentNode = nodes.SingleOrDefault(n => n.PageId.CompareToIgnoreDraftId(key));

                    if (currentNode != null)
                    {
                        if (parentNode != null)
                        {
                            currentPage.Metadata.Slug = Slug.CreateSlug(currentPage);

                            currentPage.Metadata.Url = currentPage.Metadata.Slug.Insert(0,
                                VirtualPathUtility.AppendTrailingSlash(parentNode.Url ?? ""));

                            // the currentPage has been moved so we are moving the node and rewrites the url for all child pages and the current node
                            if (parentNode.ParentId != currentPage.Parent.Id)
                            {
                                trie.MoveTo(parentNode, currentNode);

                                var ids = currentNode.Flatten(x => x.Children).Select(x => x.PageId);
                                var pages = session.Load<IPage>(ids);
                                pages.ForEach(p => { p.Metadata.Url = trie.Get(p.Id).Url; });
                            }

                            currentNode.Url = currentPage.Metadata.Url;
                        }
                    }
                    else if (parentNode != null)
                    {
                        if (parentNode.Children.All(n => n.PageId != key.Replace("/draft", "")))
                        {
                            currentPage.Metadata.Slug = Slug.CreateSlug(currentPage);

                            currentPage.Metadata.Url = currentPage.Metadata.Slug.Insert(0,
                                VirtualPathUtility.AppendTrailingSlash(parentNode.Url ?? ""));

                            parentNode.Children.Add(new TrieNode
                            {
                                PageId = key.Replace("/draft", ""),
                                ParentId = parentNode.PageId,
                                Url = currentPage.Metadata.Url
                            });
                        }
                    }
                }

                // Clean up any existing draft for this page
                if (session.Advanced.DocumentStore.Exists(key + "/draft"))
                {
                    var draft = session.Load<IPage>(key + "/draft");
                    session.Delete(draft);
                }

                session.SaveChanges();
            }
        }

        /// <summary>
        ///     Called when [page un publish].
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="currentPage">The current page.</param>
        /// <param name="metadata">The metadata.</param>
        internal void OnPageUnPublish(string key, IPage currentPage, RavenJObject metadata) {}

        /// <summary>
        ///     Called when [document delete].
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="page">The currentPage.</param>
        /// <param name="metadata">The metadata.</param>
        internal void OnDocumentDelete(string key, IPage page, RavenJObject metadata)
        {
            using (var session = this.DocumentStore.OpenSession())
            {
                var trie = session.Load<Trie>(TrieId);

                var node = trie.Get(key);

                if (node != null)
                {
                    trie.Delete(node);
                }

                // Clean up any existing draft for this page
                if (session.Advanced.DocumentStore.Exists(key + "/draft"))
                {
                    var draft = session.Load<IPage>(key + "/draft");
                    session.Delete(draft);
                }

                session.SaveChanges();
            }
        }

        /// <summary>
        ///     Registers the custom route.
        /// </summary>
        /// <param name="routes">The routes.</param>
        protected void RegisterCustomRoutes(RouteCollection routes)
        {
            // ensure that the the PageRoute is first in the collection
            routes.Insert(0,
                new DefaultRoute(
                    new VirtualPathResolver(),
                    new RouteResolver(),
                    () => this.DocumentStore,
                    new ControllerMapper()));
        }

        /// <summary>
        ///     Configures the application container with registrations needed for BrickPile to work properly
        /// </summary>
        /// <param name="existingContainer">The existing container.</param>
        /// <param name="documentStore">The document store.</param>
        protected void ConfigureApplicationContainerInternal(IContainer existingContainer, IDocumentStore documentStore)
        {
            existingContainer.Configure(expression =>
            {
                expression.For<IDocumentStore>()
                    .Singleton()
                    .Use(documentStore);
                expression.For<IRouteResolver>()
                    .Use<RouteResolver>();
                expression.Scan(scanner =>
                {
                    scanner.AssembliesFromApplicationBaseDirectory();
                    scanner.ExcludeNamespace("System");
                    scanner.ExcludeNamespace("Microsoft");
                    scanner.ExcludeNamespace("WebActivatorEx");
                    scanner.ExcludeNamespace("Newtonsoft");
                    scanner.ExcludeNamespace("Raven");
                    scanner.WithDefaultConventions();
                    scanner.Convention<ContentTypeRegistrationConvention>();
                });
                expression.For<IPage>()
                    .UseSpecial(
                        x =>
                            x.ConstructedBy(
                                () =>
                                    ((MvcHandler) HttpContext.Current.Handler).RequestContext.RouteData
                                        .GetCurrentPage<IPage>()));
                expression.For<INavigationContext>()
                    .UseSpecial(
                        x =>
                            x.ConstructedBy(
                                () => new NavigationContext(((MvcHandler) HttpContext.Current.Handler).RequestContext)));
            });
        }

        /// <summary>
        ///     Configures the application container with any additional registrations
        /// </summary>
        /// <param name="existingContainer">The existing container.</param>
        public virtual void ConfigureApplicationContainer(IContainer existingContainer) {}

        /// <summary>
        ///     Overrides/configures BrickPile's conventions
        /// </summary>
        /// <param name="brickPileConventions">The brick pile conventions.</param>
        public virtual void ConfigureConventions(BrickPileConventions brickPileConventions) {}

        /// <summary>
        ///     Initialises the document store.
        /// </summary>
        /// <returns></returns>
        public virtual IDocumentStore InitialiseDocumentStore()
        {
            var store = new EmbeddableDocumentStore
            {
                DataDirectory = DataDirectory
            };
            if (ConfigurationManager.ConnectionStrings[ConnectionStringName] != null)
            {
                store.ConnectionStringName = ConnectionStringName;
            }
            store.RegisterListener(new StoreListener(this.OnPagePublish, this.OnPageSave, this.OnPageUnPublish));
            store.RegisterListener(new DeleteListener(this.OnDocumentDelete));
            store.Initialize();
            IndexCreation.CreateIndexes(typeof (DefaultBrickPileBootstrapper).Assembly, store);
            return store;
        }
    }
}