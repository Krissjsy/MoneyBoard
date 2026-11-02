import test from 'node:test'
import assert from 'node:assert/strict'
import { validateE2EConfiguration, e2eSafetyConstants } from './e2e-safety.mjs'

const { MIGRATION_CONFIRMATION } = e2eSafetyConstants
const confirmation = { MONEYBOARD_E2E_ALLOW_MIGRATIONS: MIGRATION_CONFIRMATION }

test('accepts the explicit local E2E target and SQL Server connection aliases', () => {
  const canonical = validateE2EConfiguration({
    ...confirmation,
    MONEYBOARD_E2E_CONNECTION_STRING: 'Server=.;Database=MoneyBoard_E2E;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;',
  })
  assert.equal(canonical.server, '.')
  assert.equal(canonical.database, 'MoneyBoard_E2E')

  const aliased = validateE2EConfiguration({
    ...confirmation,
    MONEYBOARD_E2E_CONNECTION_STRING: 'Data Source=".";Initial Catalog={MoneyBoard_E2E};Integrated Security=SSPI;Encrypt=False;Trust Server Certificate=True;',
  })
  assert.equal(aliased.server, '.')
  assert.equal(aliased.database, 'MoneyBoard_E2E')
})

test('rejects missing explicit connection or migration authorization', () => {
  assert.throws(() => validateE2EConfiguration(confirmation), /MONEYBOARD_E2E_CONNECTION_STRING/)
  assert.throws(() => validateE2EConfiguration({
    MONEYBOARD_E2E_CONNECTION_STRING: 'Server=.;Database=MoneyBoard_E2E;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;',
  }), /MONEYBOARD_E2E_ALLOW_MIGRATIONS/)
})

test('rejects remote, unexpected, ambiguous, and unsupported connection targets', () => {
  const unsafe = [
    'Server=remote;Database=MoneyBoard_E2E;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;',
    'Server=.;Database=MoneyBoard;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;',
    'Server=.;Data Source=.;Database=MoneyBoard_E2E;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;',
    'Server=.;Database=MoneyBoard_E2E;User ID=some-user;Password=some-password;Encrypt=False;TrustServerCertificate=True;',
    'Server=.;Database=MoneyBoard_E2E;AttachDbFilename=other.mdf;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;',
    'Server=.;Database=MoneyBoard_E2E;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True;',
  ]
  for (const connectionString of unsafe)
    assert.throws(() => validateE2EConfiguration({ ...confirmation, MONEYBOARD_E2E_CONNECTION_STRING: connectionString }))
})

test('rejects malformed quoted and duplicate connection properties', () => {
  assert.throws(() => validateE2EConfiguration({
    ...confirmation,
    MONEYBOARD_E2E_CONNECTION_STRING: 'Server=".;Database=MoneyBoard_E2E;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;',
  }), /quoted value/)
  assert.throws(() => validateE2EConfiguration({
    ...confirmation,
    MONEYBOARD_E2E_CONNECTION_STRING: 'Server=.;Database=MoneyBoard_E2E;Initial Catalog=MoneyBoard_E2E;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;',
  }), /duplicate or aliased/)
})
