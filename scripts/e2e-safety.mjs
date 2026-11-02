const DATABASE_NAME = 'MoneyBoard_E2E'
const MIGRATION_CONFIRMATION = 'I_ALLOW_MONEYBOARD_E2E_MIGRATIONS'

const aliases = new Map([
  ['server', 'server'], ['datasource', 'server'], ['addr', 'server'], ['address', 'server'], ['networkaddress', 'server'],
  ['database', 'database'], ['initialcatalog', 'database'],
  ['trustedconnection', 'integratedsecurity'], ['integratedsecurity', 'integratedsecurity'],
  ['encrypt', 'encrypt'], ['trustservercertificate', 'trustservercertificate'],
])

function splitConnectionParts(value) {
  const parts = []
  let start = 0
  let quote = ''
  for (let index = 0; index < value.length; index++) {
    const character = value[index]
    if (quote) {
      if (character === quote) {
        if (value[index + 1] === quote) index++
        else quote = ''
      }
    } else if (character === ';') {
      parts.push(value.slice(start, index))
      start = index + 1
    } else if ((character === '"' || character === "'" || character === '{') && value.slice(start, index).includes('=')) {
      quote = character === '{' ? '}' : character
    }
  }
  if (quote) throw new Error('The E2E SQL connection string has an unclosed quoted value.')
  parts.push(value.slice(start))
  return parts
}

function readValue(text) {
  const value = text.trim()
  if (!value) return ''
  const close = value[0] === '{' ? '}' : value[0]
  if (value[0] !== '"' && value[0] !== "'" && value[0] !== '{') return value
  let result = ''
  for (let index = 1; index < value.length; index++) {
    if (value[index] === close) {
      if (value[index + 1] === close) { result += close; index++ }
      else {
        if (value.slice(index + 1).trim()) throw new Error('The E2E SQL connection string has invalid text after a quoted value.')
        return result
      }
    } else result += value[index]
  }
  throw new Error('The E2E SQL connection string has an unclosed quoted value.')
}

export function validateE2EConfiguration(environment) {
  const connectionString = environment.MONEYBOARD_E2E_CONNECTION_STRING
  if (!connectionString?.trim()) throw new Error('Set MONEYBOARD_E2E_CONNECTION_STRING to the dedicated local E2E SQL connection.')
  if (environment.MONEYBOARD_E2E_ALLOW_MIGRATIONS !== MIGRATION_CONFIRMATION)
    throw new Error(`Set MONEYBOARD_E2E_ALLOW_MIGRATIONS=${MIGRATION_CONFIRMATION} to explicitly permit migrations on the E2E database.`)

  const values = new Map()
  for (const part of splitConnectionParts(connectionString)) {
    if (!part.trim()) continue
    const separator = part.indexOf('=')
    if (separator < 1) throw new Error('The E2E SQL connection string contains an invalid property.')
    const key = part.slice(0, separator).trim().toLowerCase().replace(/[\s_-]/g, '')
    const canonical = aliases.get(key)
    if (!canonical) throw new Error('The E2E SQL connection string contains a property that is not permitted.')
    if (values.has(canonical)) throw new Error('The E2E SQL connection string contains duplicate or aliased properties.')
    values.set(canonical, readValue(part.slice(separator + 1)))
  }

  if (values.get('server') !== '.') throw new Error('E2E SQL is restricted to the local default SQL Server instance (Server=.).')
  if (values.get('database') !== DATABASE_NAME) throw new Error(`E2E SQL must target exactly ${DATABASE_NAME}.`)
  if (!['true', 'sspi'].includes((values.get('integratedsecurity') ?? '').toLowerCase()))
    throw new Error('E2E SQL must use Windows integrated authentication.')
  if ((values.get('encrypt') ?? '').toLowerCase() !== 'false') throw new Error('E2E SQL must explicitly set Encrypt=False for the local instance.')
  if ((values.get('trustservercertificate') ?? '').toLowerCase() !== 'true')
    throw new Error('E2E SQL must explicitly set TrustServerCertificate=True for the local instance.')

  return { connectionString, database: DATABASE_NAME, server: '.' }
}

export const e2eSafetyConstants = Object.freeze({ DATABASE_NAME, MIGRATION_CONFIRMATION })
