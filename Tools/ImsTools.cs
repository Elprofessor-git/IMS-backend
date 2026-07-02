namespace Backend_Gestion_Magasin_API.Tools
{
    /// <summary>
    /// Définitions des 6 outils exposés au modèle Groq.
    /// RÈGLE : tous ces outils sont en lecture seule — aucune mutation de données.
    /// </summary>
    public static class ImsTools
    {
        public static readonly object[] All =
        [
            new {
                type = "function",
                function = new {
                    name = "get_articles",
                    description =
                        "Recherche des articles dans le catalogue IMS. Retourne désignation, référence, " +
                        "catégorie, unité, seuils d'alerte/critique et stock disponible. " +
                        "À appeler en priorité pour trouver un article ou obtenir son ID avant d'appeler get_stock.",
                    parameters = new {
                        type = "object",
                        properties = new {
                            recherche = new {
                                type = "string",
                                description =
                                    "Terme de recherche dans la désignation, la référence ou la catégorie. " +
                                    "Si omis, retourne les 30 premiers articles actifs."
                            }
                        },
                        required = Array.Empty<string>()
                    }
                }
            },

            new {
                type = "function",
                function = new {
                    name = "get_stock",
                    description =
                        "Retourne le stock d'un article spécifique : quantité totale, quantité réservée et " +
                        "quantité disponible (= totale − réservée), ventilé par type de stock. " +
                        "Nécessite l'articleId obtenu via get_articles.",
                    parameters = new {
                        type = "object",
                        properties = new {
                            articleId = new {
                                type = "integer",
                                description = "ID de l'article (obligatoire)."
                            }
                        },
                        required = new[] { "articleId" }
                    }
                }
            },

            new {
                type = "function",
                function = new {
                    name = "get_commandes",
                    description =
                        "Liste les commandes clients avec client, marque, statut, montant total, " +
                        "date de livraison souhaitée et taux de couverture des ressources. " +
                        "Filtrage optionnel par statut et/ou marque.",
                    parameters = new {
                        type = "object",
                        properties = new {
                            statut = new {
                                type = "string",
                                description = "Statut de la commande : EnAttente, Prete, EnProduction, Terminee (optionnel)."
                            },
                            marqueId = new {
                                type = "integer",
                                description = "ID de la marque pour filtrer les commandes (optionnel)."
                            }
                        },
                        required = Array.Empty<string>()
                    }
                }
            },

            new {
                type = "function",
                function = new {
                    name = "get_achats",
                    description =
                        "Liste les achats locaux auprès des fournisseurs : numéro, fournisseur, statut, " +
                        "montant total, date de livraison prévue. Filtrage optionnel par statut et/ou fournisseur.",
                    parameters = new {
                        type = "object",
                        properties = new {
                            statut = new {
                                type = "string",
                                description = "Statut : Brouillon, Soumis, Confirme, Livre, Annule (optionnel)."
                            },
                            fournisseurId = new {
                                type = "integer",
                                description = "ID du fournisseur pour filtrer les achats (optionnel)."
                            }
                        },
                        required = Array.Empty<string>()
                    }
                }
            },

            new {
                type = "function",
                function = new {
                    name = "get_importations",
                    description =
                        "Liste les importations de marchandises : référence, fournisseur, statut, " +
                        "montant total, devise, date. Filtrage optionnel par statut.",
                    parameters = new {
                        type = "object",
                        properties = new {
                            statut = new {
                                type = "string",
                                description = "Statut : Brouillon, Soumise, Validee, EnTransit, Recue, Annulee (optionnel)."
                            }
                        },
                        required = Array.Empty<string>()
                    }
                }
            },

            new {
                type = "function",
                function = new {
                    name = "get_mouvements",
                    description =
                        "Historique des mouvements de stock : type (Entree/Sortie/Transfert/Ajustement…), " +
                        "quantité avant/après, motif et date. Filtrable par article et/ou plage de dates. " +
                        "Utile pour tracer l'historique d'un article ou auditer une période.",
                    parameters = new {
                        type = "object",
                        properties = new {
                            articleId = new {
                                type = "integer",
                                description =
                                    "ID de l'article à filtrer (optionnel). " +
                                    "Si omis, retourne les 50 mouvements les plus récents tous articles confondus."
                            },
                            dateDebut = new {
                                type = "string",
                                description = "Date de début au format YYYY-MM-DD (optionnel)."
                            },
                            dateFin = new {
                                type = "string",
                                description = "Date de fin au format YYYY-MM-DD (optionnel, incluse)."
                            }
                        },
                        required = Array.Empty<string>()
                    }
                }
            },
        ];
    }
}
