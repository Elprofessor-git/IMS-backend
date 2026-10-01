-- Graine de démonstration du module Courriels (voir frontend/e2e/README-courriels.md).
-- Elle crée une connexion Gmail FACTICE et des messages qui NE PASSENT PAS par l'API
-- Google : aucun appel Gmail réel ne doit être déclenché à partir de ces données.
-- Données de démonstration pour la validation E2E du module Courriels.
-- Connexion Gmail factice : aucun appel réel à l'API Gmail ne sera fait (les actions
-- d'étiquette ne sont pas exercées par ce scénario, seul le rendu l'est).
INSERT INTO "GmailConnections" ("Id","UserId","GmailAddress","GoogleUserId","RefreshTokenEncrypted","GrantedScopes","ConnectedAt","IsActive")
VALUES (1, 'e2f9bf8e-9536-4800-9763-c05f80672510', 'admin@gestiontextile.com', 'google-e2e', 'jeton-chiffre-e2e',
        'https://www.googleapis.com/auth/gmail.modify', now() - interval '30 days', true)
ON CONFLICT DO NOTHING;

-- Fil 1 : trois messages, dont un HTML hostile et une image intégrée (cid:).
INSERT INTO "GmailMessages"
 ("Id","GmailConnectionId","GmailMessageId","GmailThreadId","From","To","Subject","BodyText","BodyHtml","Snippet","ReceivedAt","IsRead","IsStarred","HasAttachments","LabelsJson","IsSynchronized","LastSyncedAt","Rfc822MessageId")
VALUES
 (1, 1, 'e2e-msg-1', 'e2e-thread-1', '"Marie Dubois" <marie@client-exemple.fr>', 'admin@gestiontextile.com',
  'Commande 4821 — tissu polyester', 'Bonjour, pouvez-vous confirmer la livraison ?',
  '<div style="font-family:Arial"><p>Bonjour,</p><p>Merci de confirmer la livraison du tissu.</p><img src="cid:logo-e2e" width="80"><script>window.alert("XSS")</script><a href="javascript:alert(1)">Lien piégé</a><iframe src="https://exemple.fr"></iframe></div>',
  'Bonjour, pouvez-vous confirmer la livraison ?', now() - interval '3 days', false, true, true,
  '["INBOX","UNREAD","CATEGORIAL_UPDATES"]', true, now(), 'rfc822-e2e-1'),
 (2, 1, 'e2e-msg-2', 'e2e-thread-1', 'admin@gestiontextile.com', 'marie@client-exemple.fr',
  'Re : Commande 4821 — tissu polyester', 'Bonjour Marie, livraison confirmée.',
  '<p>Bonjour Marie, <strong>livraison confirmée</strong>.</p>', 'Bonjour Marie, livraison confirmée.',
  now() - interval '2 days', true, false, false, '["INBOX"]', true, now(), 'rfc822-e2e-2'),
 (3, 1, 'e2e-msg-3', 'e2e-thread-1', '"Marie Dubois" <marie@client-exemple.fr>', 'admin@gestiontextile.com',
  'Re : Commande 4821 — tissu polyester', 'Parfait, merci beaucoup !',
  '<p>Parfait, merci beaucoup&nbsp;!</p>', 'Parfait, merci beaucoup !', now() - interval '1 day',
  false, false, false, '["INBOX","UNREAD"]', true, now(), 'rfc822-e2e-3'),
 -- Fil 2 : un message avec une pièce jointe « classique ».
 (4, 1, 'e2e-msg-4', 'e2e-thread-2', '"Service Commercial" <devis@fournisseur-exemple.fr>', 'admin@gestiontextile.com',
  'Devis lot 2024-118', 'Veuillez trouver notre devis ci-joint.', NULL, 'Veuillez trouver notre devis ci-joint.',
  now() - interval '5 hours', true, false, true, '["INBOX"]', true, now(), 'rfc822-e2e-4'),
 -- Fil 3 : message déjà lu, sans HTML, pour l'état vide de la liste en filtrage.
 (5, 1, 'e2e-msg-5', 'e2e-thread-3', '"Newsletter" <news@exemple.fr>', 'admin@gestiontextile.com',
  'Actualités du mois', 'Contenu de la newsletter.', NULL, 'Contenu de la newsletter.',
  now() - interval '10 days', true, false, false, '["INBOX"]', true, now(), 'rfc822-e2e-5');

-- Pièces du message 4 : une image intégrée (servie par le proxy inline) et un PDF téléchargeable.
INSERT INTO "GmailAttachments"
 ("Id","GmailMessageId","GmailAttachmentId","FileName","MimeType","SizeBytes","IsInline","ContentId","CreatedAt")
VALUES
 (1, 1, 'att-e2e-inline', 'logo.png', 'image/png', 2048, true, 'logo-e2e', now()),
 (2, 1, 'att-e2e-cid-pdf', 'plan-de-coupe.pdf', 'application/pdf', 40960, true, 'plan-e2e', now()),
 (3, 4, 'att-e2e-devis', 'devis-2024-118.pdf', 'application/pdf', 153600, false, NULL, now());

-- Les sequences sont advanced à la main : l'insertion utilise des Id explicites, et
-- l'application Apache\EntityFrameworkCore ne les replacerait pas toute seule.
SELECT setval(pg_get_serial_sequence('"GmailMessages"', 'Id'), 100);
SELECT setval(pg_get_serial_sequence('"GmailAttachments"', 'Id'), 100);
SELECT setval(pg_get_serial_sequence('"GmailConnections"', 'Id'), 100);
