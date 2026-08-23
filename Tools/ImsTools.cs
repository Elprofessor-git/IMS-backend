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
                        "Liste les commandes clients avec client, plateforme, statut, montant total, " +
                        "date de livraison souhaitée et taux de couverture des ressources. " +
                        "Filtrage optionnel par statut, client (nom) ou plateforme (nom). " +
                        "Exemple : « commandes pour dandy's » → plateformeNom = \"dandy's\".",
                    parameters = new {
                        type = "object",
                        properties = new {
                            statut = new {
                                type = "string",
                                description = "Statut de la commande : EnAttente, Prete, EnProduction, Terminee, Annulee (optionnel)."
                            },
                            clientNom = new {
                                type = "string",
                                description = "Nom (ou partie) du client pour filtrer les commandes (optionnel)."
                            },
                            plateformeNom = new {
                                type = "string",
                                description =
                                    "Nom (ou partie) de la plateforme pour filtrer les commandes (optionnel). " +
                                    "Exemple : \"dandy's\", \"vinted\", \"ebay\"."
                            },
                            dateDebut = new {
                                type = "string",
                                description = "Date de début au format YYYY-MM-DD (optionnel) — filtre sur la date de création."
                            },
                            dateFin = new {
                                type = "string",
                                description = "Date de fin au format YYYY-MM-DD (optionnel, incluse) — filtre sur la date de création."
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
                        "montant total, date achat, date livraison prévue, commandé par, et les lignes d'achat " +
                        "(articles, désignations, quantités, prix, plateforme). " +
                        "Filtrage combinable par statut, fournisseur (nom), plateforme (nom), article (nom) et période. " +
                        "Exemple : « bobines achetées en avril pour dandy's » → plateformeNom = \"dandy's\", " +
                        "dateDebut = \"2026-04-01\", dateFin = \"2026-04-30\".",
                    parameters = new {
                        type = "object",
                        properties = new {
                            statut = new {
                                type = "string",
                                description = "Statut : Brouillon, Soumis, Confirme, Livre, Annule (optionnel)."
                            },
                            fournisseurNom = new {
                                type = "string",
                                description = "Nom (ou partie) du fournisseur pour filtrer les achats (optionnel)."
                            },
                            plateformeNom = new {
                                type = "string",
                                description =
                                    "Nom (ou partie) de la plateforme pour filtrer les achats (optionnel). " +
                                    "Exemple : \"dandy's\", \"vinted\", \"ebay\"."
                            },
                            articleNom = new {
                                type = "string",
                                description =
                                    "Nom (ou partie) de l'article pour filtrer les achats (optionnel). " +
                                    "Exemple : \"bobine\", \"bouton\", \"pion\"."
                            },
                            dateDebut = new {
                                type = "string",
                                description = "Date de début au format YYYY-MM-DD (optionnel) — filtre sur la date d'achat."
                            },
                            dateFin = new {
                                type = "string",
                                description = "Date de fin au format YYYY-MM-DD (optionnel, incluse) — filtre sur la date d'achat."
                            },
                            fournisseurId = new {
                                type = "integer",
                                description = "ID numérique exact du fournisseur (optionnel). Préférez fournisseurNom si l'utilisateur donne un nom."
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
                        "montant total, devise, dates de réception, mode d'expédition, commandé par, " +
                        "et les lignes (articles, quantités, prix, plateforme). Filtrage optionnel par " +
                        "fournisseur (NOM, pas d'ID), plateforme (NOM), article, plage de dates et mode d'expédition.",
                    parameters = new {
                        type = "object",
                        properties = new {
                            statut = new {
                                type = "string",
                                description = "Statut : Brouillon, Soumise, Validee, EnTransit, Recue, Annulee (optionnel)."
                            },
                            fournisseurNom = new {
                                type = "string",
                                description = "Nom du fournisseur (optionnel). Filtrer par nom, jamais par ID."
                            },
                            plateformeNom = new {
                                type = "string",
                                description = "Nom de la plateforme de destination (optionnel)."
                            },
                            articleNom = new {
                                type = "string",
                                description = "Nom ou référence de l'article (optionnel)."
                            },
                            dateDebut = new {
                                type = "string",
                                description = "Date de début (AAAA-MM-JJ) pour filtrer sur la date de l'importation (optionnel)."
                            },
                            dateFin = new {
                                type = "string",
                                description = "Date de fin (AAAA-MM-JJ) pour filtrer sur la date de l'importation (optionnel)."
                            },
                            modeExpedition = new {
                                type = "string",
                                description = "Mode d'expédition : Maritime, Aerien, Terrestre, Express, Autre (optionnel)."
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

            new {
                type = "function",
                function = new {
                    name = "get_schema",
                    description =
                        "Décrit le MODÈLE DE DONNÉES de l'IMS : entités, relations entre elles " +
                        "(hiérarchies comme Plateforme → Client → Commande → Achat), enums de statut " +
                        "et champs clés. À appeler quand tu n'es pas sûr de l'entité concernée par la " +
                        "question ou des relations (ex : la plateforme d'un achat peut venir des lignes " +
                        "OU de la commande liée). Retourne le schéma complet, ou une partie si 'sujet' est fourni.",
                    parameters = new {
                        type = "object",
                        properties = new {
                            sujet = new {
                                type = "string",
                                description =
                                    "Sujet à détailler (ex : 'achat', 'importation', 'commande', 'stock', " +
                                    "'plateforme', 'fournisseur'). Si omis, retourne le schéma complet."
                            }
                        },
                        required = Array.Empty<string>()
                    }
                }
            },
        ];
    }
}
