-- ============================================================
-- SafeVault — Demo Seed Data
-- Passwords (BCrypt workfactor 12):
--   admin@safevault.io   → Admin1234!
--   manager@safevault.io → Manager1234!
--   viewer@safevault.io  → Viewer1234!
-- ============================================================

-- Users
INSERT INTO "Users" ("Id","Email","PasswordHash","Role","IsActive","FailedLoginAttempts","LockoutUntilUtc","CreatedAtUtc","LastLoginAtUtc")
VALUES
(
  'a1000000-0000-0000-0000-000000000001',
  'admin@safevault.io',
  '$2a$12$Y9K2V3nqL8pQw1mXeT5O6.HzRqUvWxNbPdCjFkGs7tIlMoAe4uYri',
  'Admin',
  TRUE, 0, NULL,
  NOW(), NULL
),
(
  'a2000000-0000-0000-0000-000000000002',
  'manager@safevault.io',
  '$2a$12$D4R7J1kMcN2sP9vYqU6B8.WfXoLtZeAmCgHiEbFn5uKpOdVs3wQjy',
  'Manager',
  TRUE, 0, NULL,
  NOW(), NULL
),
(
  'a3000000-0000-0000-0000-000000000003',
  'viewer@safevault.io',
  '$2a$12$G8T5M2oNdP3rQ7uYwV9C1.XgZnKsAeBmDhJlFcIe6vLqPfUs4xRkt',
  'Viewer',
  TRUE, 0, NULL,
  NOW(), NULL
);

-- Vaults
INSERT INTO "Vaults" ("Id","OwnerId","Name","Description","DirectoryPath","RetentionDays","AutoDeleteOnExpiry","IsArchived","CreatedAtUtc")
VALUES
(
  'b1000000-0000-0000-0000-000000000001',
  'a1000000-0000-0000-0000-000000000001',
  'Vault Principal',
  'Cofre principal do administrador',
  'vaults/b1000000-0000-0000-0000-000000000001',
  365, FALSE, FALSE,
  NOW()
),
(
  'b2000000-0000-0000-0000-000000000002',
  'a2000000-0000-0000-0000-000000000002',
  'Vault Projectos',
  'Documentos de projectos internos',
  'vaults/b2000000-0000-0000-0000-000000000002',
  180, FALSE, FALSE,
  NOW()
);

-- VaultAccesses (manager e viewer têm acesso ao vault principal)
INSERT INTO "VaultAccesses" ("Id","VaultId","UserId","GrantedBy","GrantedAtUtc","AccessLevel")
VALUES
(
  'c1000000-0000-0000-0000-000000000001',
  'b1000000-0000-0000-0000-000000000001',
  'a2000000-0000-0000-0000-000000000002',
  'a1000000-0000-0000-0000-000000000001',
  NOW(), 'Write'
),
(
  'c2000000-0000-0000-0000-000000000002',
  'b1000000-0000-0000-0000-000000000001',
  'a3000000-0000-0000-0000-000000000003',
  'a1000000-0000-0000-0000-000000000001',
  NOW(), 'Read'
);

-- Documents
INSERT INTO "Documents" ("Id","VaultId","UploadedBy","OriginalFileName","StoredFileName","FilePath","MimeType","FileSize","Sha256Hash","Classification","IsDeleted","DeletedAtUtc","CreatedAtUtc")
VALUES
(
  'd1000000-0000-0000-0000-000000000001',
  'b1000000-0000-0000-0000-000000000001',
  'a1000000-0000-0000-0000-000000000001',
  'relatorio_anual.pdf', 'relatorio_anual_v1.pdf',
  'vaults/b1000000-0000-0000-0000-000000000001/relatorio_anual_v1.pdf',
  'application/pdf', 204800,
  'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855',
  'Confidential', FALSE, NULL, NOW()
),
(
  'd2000000-0000-0000-0000-000000000002',
  'b1000000-0000-0000-0000-000000000001',
  'a2000000-0000-0000-0000-000000000002',
  'politica_seguranca.pdf', 'politica_seguranca_v1.pdf',
  'vaults/b1000000-0000-0000-0000-000000000001/politica_seguranca_v1.pdf',
  'application/pdf', 102400,
  'a665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae3',
  'Internal', FALSE, NULL, NOW()
),
(
  'd3000000-0000-0000-0000-000000000003',
  'b2000000-0000-0000-0000-000000000002',
  'a2000000-0000-0000-0000-000000000002',
  'readme_publico.txt', 'readme_publico_v1.txt',
  'vaults/b2000000-0000-0000-0000-000000000002/readme_publico_v1.txt',
  'text/plain', 1024,
  'b94f6f125c79e3a5ffaa826f584c10d52ada669e6762051b826b55776d05a8d4',
  'Public', FALSE, NULL, NOW()
);

-- AuditLogs de exemplo
INSERT INTO "AuditLogs" ("Id","EventType","UserId","TargetResourceId","TargetResourceType","IpAddress","UserAgent","TimestampUtc","Success","Details")
VALUES
(
  'e1000000-0000-0000-0000-000000000001',
  'UserLogin',
  'a1000000-0000-0000-0000-000000000001',
  NULL,
  'User',
  '127.0.0.1',
  'Mozilla/5.0 (seed)',
  NOW() - INTERVAL '1 hour',
  TRUE,
  'Admin login via seed'
),
(
  'e2000000-0000-0000-0000-000000000002',
  'DocumentUploaded',
  'a1000000-0000-0000-0000-000000000001',
  'd1000000-0000-0000-0000-000000000001',
  'Document',
  '127.0.0.1',
  'Mozilla/5.0 (seed)',
  NOW() - INTERVAL '30 minutes',
  TRUE,
  'relatorio_anual.pdf uploaded via seed'
);
