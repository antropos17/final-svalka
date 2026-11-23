import Phaser from 'phaser';
import { App } from '@capacitor/app';
import { Capacitor } from '@capacitor/core';
import { economy, type State } from './core/model';
import { GameStore, PlatformStorage, SaveError } from './platform/save';
import { GameScene } from './world/GameScene';
import './style.css';

document.querySelector<HTMLDivElement>('#app')!.innerHTML = `
  <div id="game" aria-hidden="true"></div>
  <header><div class="brand">СВАЛКА <span>→ НЕОН</span></div><div class="topline"><span class="chapter">01 / НОВОЕ НАЧАЛО</span><button id="info" class="icon" aria-label="Правила и сохранение">?</button></div><div class="wallet"><span class="coin" aria-hidden="true">✦</span><strong id="coins">—</strong><span>МОНЕТЫ</span></div></header>
  <section class="world-label"><span id="stage">ЗАБРОШЕННЫЙ ДВОР</span><h1 id="house-name">У каждого места<br>есть будущее.</h1></section>
  <button id="project" class="project"><span><small id="project-eyebrow">ПЕРВЫЙ ПРОЕКТ</small><strong id="project-label">Починить дом</strong></span><span class="project-price" id="price">40 <b>↗</b></span></button>
  <section class="controls"><div class="machine-label"><span>МАСТЕРСКАЯ УДАЧИ</span><span>3 / 3</span></div><div class="reel-space"></div><p id="message" role="status" aria-live="polite">Загрузка двора…</p><div class="buttons"><button id="auto" class="auto" disabled aria-pressed="false">АВТО<span>после ремонта</span></button><button id="spin" class="spin" disabled>КРУТИТЬ <span>↻</span></button></div><small class="free">БЕСПЛАТНО · КАЖДЫЙ РЕЗУЛЬТАТ ПРИНОСИТ МОНЕТЫ</small></section>
  <dialog id="panel" aria-labelledby="panel-title"><form method="dialog"><button class="close" aria-label="Закрыть">×</button></form><p class="kicker">ВАШ СЛЕДУЮЩИЙ ШАГ</p><h2 id="panel-title"></h2><p id="panel-copy"></p><button id="select" class="spin">Начать проект</button><button id="restore" class="secondary" hidden>Восстановить резервную копию</button><button id="reload" class="secondary" hidden>Перезапустить</button></dialog>
  <div id="paused" hidden>Двор на паузе</div>`;
const $ = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
const scene = new GameScene();
const game = new Phaser.Game({ type: Phaser.AUTO, parent: 'game', backgroundColor: '#b9ccbb', pixelArt: true, antialias: false, scale: { mode: Phaser.Scale.RESIZE, width: 390, height: 844 }, scene: [scene], audio: { noAudio: true } });
const store = new GameStore(new PlatformStorage());
const panel = $<HTMLDialogElement>('panel');
const spin = $<HTMLButtonElement>('spin'), auto = $<HTMLButtonElement>('auto'), select = $<HTMLButtonElement>('select');
let state: State | undefined, busy = false, autoOn = false, inactive = document.hidden, failed = false;
let autoTimer: ReturnType<typeof setTimeout> | undefined;
const stopAuto = () => { autoOn = false; clearTimeout(autoTimer); auto.setAttribute('aria-pressed', 'false'); auto.innerHTML = state && state.world.house >= 1 ? 'АВТО<span>выключено</span>' : 'АВТО<span>после ремонта</span>'; };
function render(message?: string) {
  if (!state) return;
  $('coins').textContent = state.coins.toLocaleString('ru');
  const stage = state.world.house;
  $('stage').textContent = ['ЗАБРОШЕННЫЙ ДВОР', 'ЗДЕСЬ УЖЕ ЕСТЬ ЖИЗНЬ', 'ПЕРВЫЙ ОГОНЬ НЕОНА'][stage];
  $('house-name').innerHTML = ['У каждого места<br>есть будущее.', 'Дом снова<br>согревает.', 'Большие перемены<br>начинаются здесь.'][stage];
  $('project-eyebrow').textContent = stage === 2 ? 'ПЕРВЫЙ ЭТАП ЗАВЕРШЁН' : state.selected ? 'ПРОЕКТ В РАБОТЕ' : 'ВЫБЕРИТЕ ПРОЕКТ';
  $('project-label').textContent = ['Починить дом', 'Зажечь мастерскую', 'Двор оживает'][stage];
  $('price').textContent = stage < 2 ? `${Math.min(state.coins, economy.prices[stage])} / ${economy.prices[stage]} ↗` : '✓';
  if (message) $('message').textContent = message;
  spin.disabled = busy || failed || inactive;
  auto.disabled = busy && !autoOn || stage < 1 || failed || inactive;
  $<HTMLButtonElement>('project').disabled = busy || failed;
  scene.show(state);
  if (!autoOn) stopAuto();
}
function showPanel(title: string, copy: string, canSelect = false) {
  stopAuto(); $('panel-title').textContent = title; $('panel-copy').textContent = copy; select.hidden = !canSelect; select.disabled = busy;
  panel.querySelector<HTMLButtonElement>('.close')!.hidden = failed;
  if (!panel.open) panel.showModal();
}
panel.addEventListener('cancel', event => { if (failed) event.preventDefault(); });
function storageFailure(error: unknown) {
  failed = true; stopAuto(); scene.animateReels(false);
  showPanel('Сохраним ваш прогресс', error instanceof SaveError ? error.message : 'Не удалось записать прогресс. Игра остановлена. Освободите место и перезапустите её; не очищайте данные приложения.');
  $<HTMLButtonElement>('restore').hidden = !(error instanceof SaveError && error.backupAvailable);
  $('reload').hidden = false; spin.disabled = true; auto.disabled = true; $<HTMLButtonElement>('project').disabled = true;
}
async function doSpin() {
  if (!state || busy || failed || inactive || panel.open) return;
  busy = true; render('Собираем материалы…'); const start = performance.now();
  try {
    const previousStage = state.world.house;
    const next = await store.execute({ kind: 'spin' }, state.sequence + 1);
    scene.animateReels(true);
    await new Promise(resolve => setTimeout(resolve, Math.max(0, economy.spinDurationMs - (performance.now() - start))));
    state = next; scene.animateReels(false); busy = false;
    const reward = state.lastSpin!.reward;
    render(state.world.house > previousStage ? `+${reward} монет · Проект готов! Посмотрите на дом ↑` : `+${reward} монет${state.selected ? ' · Приближаемся к цели' : ' · Выберите, что построить дальше'}`);
    if (autoOn && !inactive && !panel.open) autoTimer = setTimeout(() => void doSpin(), economy.autoGapMs);
  } catch (error) { busy = false; storageFailure(error); }
}
spin.onclick = () => void doSpin();
auto.onclick = () => {
  if (autoOn) { stopAuto(); return; }
  if (busy || !state || state.world.house < 1 || failed || inactive) return;
  autoOn = true; auto.setAttribute('aria-pressed', 'true'); auto.innerHTML = 'СТОП<span>авто включено</span>'; void doSpin();
};
$('project').onclick = () => {
  if (!state || failed || busy) return;
  const stage = state.world.house;
  if (stage >= 2) { showPanel('Первый двор готов', 'Вы вернули дому жизнь и зажгли мастерскую. Это конец первого прототипа. Прогресс сохранён.'); return; }
  showPanel(stage === 0 ? 'Вернём дому жизнь' : 'Зажжём мастерскую', `${stage === 0 ? 'Починим крышу, уберём хлам и зажжём окна.' : 'Новый фасад и неоновый свет преобразят двор.'} Стоимость: ${economy.prices[stage]} монет. ${state.selected ? 'Проект уже выбран. Вращайте — покупка произойдёт, когда монет хватит.' : 'Нажмите «Начать проект». Покупка произойдёт один раз, как только монет хватит. Остаток останется у вас.'}`, !state.selected);
};
select.onclick = async () => {
  if (!state || busy || failed || state.world.house >= 2) return;
  busy = true; select.disabled = true; render();
  try { state = await store.execute({ kind: 'select', target: (state.world.house + 1) as 1 | 2 }, state.sequence + 1); panel.close(); busy = false; render(state.selected ? 'Проект выбран. Крутите — первый ремонт уже близко.' : 'Проект готов! Следующий выбираете вы.'); }
  catch (error) { busy = false; storageFailure(error); }
};
$('info').onclick = () => { if (!failed) showPanel('Маленький двор. Большое начало.', 'Вращения бесплатны: тройка даёт 23 монеты, пара — 8, разные символы — 3. Выберите проект, чтобы монеты пошли в дело. После ремонта откроется АВТО. Прогресс сохраняется до показа результата.'); };
$('reload').onclick = () => location.reload();
$('restore').onclick = async () => {
  if (!confirm('Восстановить предыдущую запись? Часть последнего прогресса может быть потеряна. Исходная запись будет сохранена отдельно.')) return;
  try { state = await store.restoreBackup(); failed = false; $('restore').hidden = true; $('reload').hidden = true; panel.close(); render('Резервная копия восстановлена.'); } catch (error) { storageFailure(error); }
};
function setActive(active: boolean) {
  inactive = !active;
  if (!active) { stopAuto(); game.loop.sleep(); } else game.loop.wake();
  $('paused').hidden = active; if (state && !failed) render();
}
document.addEventListener('visibilitychange', () => setActive(!document.hidden));
window.addEventListener('pagehide', stopAuto);
window.addEventListener('keydown', event => { if (event.key === 'Escape') stopAuto(); });
if (Capacitor.isNativePlatform()) {
  void App.addListener('appStateChange', ({ isActive }) => setActive(isActive));
  void App.addListener('backButton', () => { stopAuto(); if (panel.open) { if (!failed) panel.close(); } else if (confirm('Выйти из игры? Прогресс уже сохранён.')) void App.exitApp(); });
}
const seed = crypto.getRandomValues(new Uint32Array(1))[0] || 1;
void store.open(seed).then(saved => { state = saved; render(saved.world.house === 0 && !saved.selected ? 'Выберите ремонт дома ↑ Затем сделайте первый спин.' : 'С возвращением. Ваш двор на месте.'); }).catch(storageFailure);
