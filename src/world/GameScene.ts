import Phaser from 'phaser';
import type { Reels, State, SymbolId } from '../core/model';
const names: Record<SymbolId, string> = { potato: 'КАРТОШКА', box: 'КОРОБКА', trash: 'ХЛАМ', pigeon: 'ГОЛУБЬ' };
const glyphs: Record<SymbolId, string> = { potato: '🥔', box: '📦', trash: '♻', pigeon: '🕊' };
interface HouseAsset { path: string; atlas: string; frames: string[]; origins?: number[][] }
export class GameScene extends Phaser.Scene {
  private state?: State;
  private reelTexts: Phaser.GameObjects.Text[] = [];
  private house?: HouseAsset;
  private spinning = false;
  private redrawQueued = false;
  constructor() { super('world'); }
  preload() { this.load.json('manifest', 'manifest.json'); }
  create() {
    const manifest = this.cache.json.get('manifest') as { house?: HouseAsset } | undefined;
    this.house = manifest?.house;
    if (this.house) {
      this.load.atlas('house', this.house.path, this.house.atlas);
      this.load.once('complete', () => this.draw()); this.load.start();
    }
    this.scale.on('resize', () => { if (!this.redrawQueued) { this.redrawQueued = true; this.time.delayedCall(50, () => { this.redrawQueued = false; this.draw(); }); } });
    this.draw();
  }
  show(state: State) { this.state = state; if (this.sys.isActive()) this.draw(); }
  private draw() {
    if (!this.sys.isActive()) return;
    this.children.removeAll(true); this.reelTexts = [];
    const w = this.scale.width, h = this.scale.height;
    const g = this.add.graphics();
    g.fillStyle(0xb9ccbb); g.fillRect(0, 0, w, h * .6);
    g.fillStyle(0xdbe2c6); g.fillRect(0, h * .15, w, h * .24);
    g.fillStyle(0xa3b29c); g.fillRect(0, h * .38, w, h * .22);
    // A small quiet skyline and a paved yard give the representative asset context.
    g.fillStyle(0x8a9f91);
    for (let i = 0; i < 10; i++) { const bh = 16 + ((i * 19) % 43); g.fillRect(i * w / 9, h * .39 - bh, w / 11, bh); }
    g.fillStyle(0xc5bda0); g.fillEllipse(w * .5, h * .46, w * .96, h * .16);
    g.lineStyle(1, 0xa7a58a, .5);
    for (let i = 0; i < 7; i++) g.lineBetween(0, h * (.41 + i * .02), w, h * (.41 + i * .02));
    const stage = this.state?.world.house ?? 0;
    if (this.house && this.textures.exists('house') && this.textures.get('house').has(this.house.frames[stage])) {
      const art = this.add.image(w * .5, h * .49, 'house', this.house.frames[stage]);
      const origin = this.house.origins?.[stage] ?? [.5, 1]; art.setOrigin(origin[0], origin[1]);
      const fit = Math.min(w * .82 / art.width, h * .29 / art.height); art.setScale(fit);
    } else {
      g.fillStyle(stage ? 0xdca960 : 0x857d66); g.fillRect(w * .29, h * .31, w * .42, h * .14);
      g.fillStyle(0x3d625c); g.fillTriangle(w * .23, h * .32, w * .5, h * .24, w * .77, h * .32);
      this.add.text(w / 2, h * .37, `house · ${stage}`, { color: '#172c29', fontSize: '16px' }).setOrigin(.5);
    }
    g.fillStyle(0x172c29); g.fillRect(0, h * .6, w, h * .4);
    this.add.text(w * .13, h * .445, '🥔', { fontSize: `${w * .065}px` }).setOrigin(.5);
    this.add.text(w * .83, h * .445, '🕊', { fontSize: `${w * .065}px` }).setOrigin(.5);
    const values: Reels = this.state?.lastSpin?.symbols ?? ['potato', 'box', 'pigeon'];
    for (let i = 0; i < 3; i++) {
      const x = w * (.18 + i * .32), y = h * .706;
      g.fillStyle(0x0d211e); g.fillRoundedRect(x - w * .137, y - h * .066, w * .274, h * .132, 10);
      g.fillStyle(0xf0ead4); g.fillRoundedRect(x - w * .127, y - h * .059, w * .254, h * .112, 8);
      g.fillStyle(0xd3c9a9); g.fillRect(x - w * .127, y + h * .039, w * .254, h * .014);
      const t = this.add.text(x, y - h * .011, glyphs[values[i]], { fontFamily: 'Georgia, serif', fontSize: `${Math.min(w * .13, h * .06)}px`, color: '#31574d' }).setOrigin(.5);
      this.reelTexts.push(t);
      this.add.text(x, y + h * .03, names[values[i]], { fontFamily: 'monospace', fontSize: `${Math.max(10, w * .025)}px`, color: '#53675b' }).setOrigin(.5);
    }
  }
  animateReels(active: boolean) { this.spinning = active; if (!active) this.draw(); }
  update(time: number) {
    if (this.spinning && !matchMedia('(prefers-reduced-motion: reduce)').matches) this.reelTexts.forEach((t, i) => t.setText(Object.values(glyphs)[(Math.floor(time / 100) + i) % 4]));
  }
}
