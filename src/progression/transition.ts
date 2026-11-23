import { economy, type Pending, type State } from '../core/model';
import { draw, payout } from '../slot/spin';
export type Command = { kind: 'spin' } | { kind: 'select'; target: 1 | 2 };
export function reserve(state: State, command: Command, sequence: number): State {
  if (state.pending) throw new Error('Pending operation must be recovered first');
  if (sequence <= state.sequence) return state;
  if (sequence !== state.sequence + 1) throw new Error('Operation sequence gap');
  if (command.kind === 'select' && command.target !== state.world.house + 1) throw new Error('Project stage mismatch');
  const pending: Pending = command.kind === 'spin' ? { sequence, kind: 'spin', ...draw(state.seed) } : { sequence, ...command };
  return { ...state, revision: state.revision + 1, pending };
}
export function applyPending(state: State): State {
  if (!state.pending) return state;
  const pending = state.pending;
  if (pending.sequence !== state.sequence + 1) throw new Error('Invalid pending sequence');
  let coins = state.coins;
  let stage = state.world.house;
  let selected = state.selected;
  let seed = state.seed;
  let lastSpin = state.lastSpin;
  if (pending.kind === 'spin') {
    const reward = payout(pending.symbols);
    coins += reward; seed = pending.seedAfter;
    lastSpin = { sequence: pending.sequence, symbols: pending.symbols, reward };
  } else { selected = `house:${pending.target}`; }
  const cost = economy.prices[stage];
  if (selected === `house:${stage + 1}` && cost !== undefined && coins >= cost) {
    coins -= cost; stage++; selected = null;
  }
  if (!Number.isSafeInteger(coins)) throw new Error('Wallet overflow');
  return { ...state, revision: state.revision + 1, sequence: pending.sequence, coins, world: { house: stage }, selected, seed, lastSpin, pending: null };
}
