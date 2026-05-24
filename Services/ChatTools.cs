namespace Backend_Gestion_Magasin_API.Services
{
    public static class ChatTools
    {
        public static readonly object[] All = new object[]
        {
            new {
                type = "function",
                function = new {
                    name = "get_articles",
                    description = "Récupère la liste des articles du magasin. Peut filtrer par catégorie ou terme de recherche.",
                    parameters = new {
                        type = "object",
                        properties = new {
                            categorie = new { type = "string", description = "Catégorie de l'article (ex: Tissu, Fil, Bouton, Accessoire)" },
                            searchTerm = new { type = "string", description = "Terme de recherche dans la désignation ou référence" }
                        },
                        required = new string[] {}
                    }
                }
            },
            new {
                type = "function",
                function = new {
                    name = "get_article_stock",
                    description = "Récupère le stock total d'un article spécifique par son ID.",
                    parameters = new {
                        type = "object",
                        properties = new {
                            articleId = new { type = "integer", description = "ID de l'article" }
                        },
                        required = new[] { "articleId" }
                    }
                }
            },
            new {
                type = "function",
                function = new {
                    name = "get_stock_alertes",
                    description = "Récupère tous les articles dont le stock est sous le seuil d'alerte ou critique.",
                    parameters = new {
                        type = "object",
                        properties = new {},
                        required = new string[] {}
                    }
                }
            },
            new {
                type = "function",
                function = new {
                    name = "get_commandes",
                    description = "Récupère la liste des commandes clients. Peut filtrer par statut.",
                    parameters = new {
                        type = "object",
                        properties = new {
                            statut = new { type = "string", description = "Statut de la commande : EnAttente, Prete, EnProduction, Terminee" }
                        },
                        required = new string[] {}
                    }
                }
            },
            new {
                type = "function",
                function = new {
                    name = "get_commande_detail",
                    description = "Récupère le détail complet d'une commande client : besoins, tâches, achats associés.",
                    parameters = new {
                        type = "object",
                        properties = new {
                            commandeId = new { type = "integer", description = "ID de la commande client" }
                        },
                        required = new[] { "commandeId" }
                    }
                }
            },
            new {
                type = "function",
                function = new {
                    name = "get_importations",
                    description = "Récupère la liste des importations. Peut filtrer par statut ou fournisseur.",
                    parameters = new {
                        type = "object",
                        properties = new {
                            statut = new { type = "string", description = "Statut : Brouillon, Soumise, Validee, Recue" },
                            fournisseurId = new { type = "integer", description = "ID du fournisseur" }
                        },
                        required = new string[] {}
                    }
                }
            },
            new {
                type = "function",
                function = new {
                    name = "get_achats",
                    description = "Récupère la liste des achats locaux. Peut filtrer par statut ou commande client.",
                    parameters = new {
                        type = "object",
                        properties = new {
                            statut = new { type = "string", description = "Statut : Brouillon, Soumis, Confirme, Livre" },
                            commandeId = new { type = "integer", description = "ID de la commande client associée" }
                        },
                        required = new string[] {}
                    }
                }
            }
        };
    }
}
