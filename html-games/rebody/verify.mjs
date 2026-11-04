import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { Script, createContext } from 'node:vm';

// Exercise the shipped inline game itself, without dependencies or a web server.
// The fake DOM only supplies rendering surfaces; combat and navigation are real.
const html = readFileSync(new URL('./index.html', import.meta.url), 'utf8');
const source = html.match(/<script>([\s\S]*?)<\/script>/)[1];
const noop = () => {};
const context2d = new Proxy({
  measureText: text => ({ width: text.length * 6 }),
  createRadialGradient: () => ({ addColorStop: noop })
}, { get: (obj, key) => key in obj ? obj[key] : noop });
const elements = new Map();
const handlers = new Map();
function element(id) {
  if (!elements.has(id)) elements.set(id, {
    id, textContent: '', innerHTML: '', hidden: false, style: {}, children: [],
    classList: { add: noop, remove: noop, toggle: noop },
    setAttribute: noop, addEventListener: noop, focus: noop, setPointerCapture: noop,
    getBoundingClientRect: () => ({ x: 0, y: 0, left: 0, top: 0, width: 1120, height: 720 }),
    getContext: () => context2d
  });
  return elements.get(id);
}
element('hpTrack').children = [element('hp0'), element('hp1'), element('hp2')];
const storage = new Map();
const testMath=Object.create(Math);testMath.random=Math.random;
const sandbox = { Math:testMath,
  console, setTimeout: noop, requestAnimationFrame: noop,
  ResizeObserver: class { observe() {} },
  window: { matchMedia: () => ({ matches: false }), addEventListener: noop, devicePixelRatio: 1 },
  localStorage: { getItem: k => storage.get(k), setItem: (k, v) => storage.set(k, v) },
  document: {
    getElementById: element, createElement: id => element('created-' + id),
    querySelectorAll: () => [element('ch0'), element('ch1'), element('ch2')],
    addEventListener: (type, fn) => handlers.set(type, fn)
  }
};
const exposure = `globalThis.api = { LEVELS, MODULES, loadLevel, beginRun, retry, nextLevel, finishRun,
  solid, visible, moveBody, updateFlow, chase, slash, fire, spinAttack, dodge, interact, wear,
  hurtPlayer, hitEnemy, killEnemy, update, updateBullets, draw, pauseGame, resume,
  LIMBS, beginTargeting, chooseLimb, cancelTargeting, finishExecution, limbPosition, limbQuality,
  DOWN_DURATION, downEnemy, executionTarget, tryExecution, updateEnemies, bodyPoint, limbJoint, drawProneActor,
  cameraView, screenToWorld, pickLimbAt, camera, worldTimeScale, BODY_STYLES, localLimb, availableLimbs, missingParts, hasModule, throwArm, updateThrownArms, explodeArm,
 get thrownArms(){return thrownArms}, get looseArms(){return looseArms},
  isOnExecutionBody,executionProfile,executionAnchor,executionPose,meleeReach,playerPartPosition,drawExecutionActors,
  get focus(){return focus}, get corpses(){return corpses},
  get state(){return state}, set state(v){state=v}, get player(){return player},
  get enemies(){return enemies}, set enemies(v){enemies=v},
  get bullets(){return bullets}, get flow(){return flow}, get bloodStains(){return bloodStains}, get particles(){return particles},
  get score(){return score}, set score(v){score=v}, get runTime(){return runTime},
  set runTime(v){runTime=v}, get checkpoint(){return checkpoint},
  get levelIndex(){return levelIndex},
  get walls(){return walls}, get props(){return props}, get hitStop(){return hitStop},
  keys, mouse, get combo(){return combo}, get installs(){return installs},get kills(){return kills}
};`;
const code = source.replace(/\}\)\(\);\s*$/, exposure + '\n})();');
const ctx = createContext(sandbox);
new Script(code).runInContext(ctx);
const g = sandbox.api;
let passed = 0;
function test(name, fn) { fn(); passed++; console.log('PASS ' + name); }
function reset() { g.beginRun(); g.enemies.forEach(e => { e.cool = 100; }); }
function equip(type,value=100){g.player.modules[type]=value;g.player.componentKinds[type]='salvaged';g.player.attached[type]=true;}
function openSpace(x = 550, y = 580) { g.player.x = x; g.player.y = y; }
function advance(seconds) { for (let i = 0; i < Math.ceil(seconds * 60); i++) g.update(1 / 60); }
function knockDown() { openSpace(); g.player.a = 0; const e = g.enemies[0]; e.x = 592; e.y = 580; g.slash(); return e; }
function mountTarget(e){const p=g.bodyPoint(e,0,8);g.player.x=p.x;g.player.y=p.y;}
function engage() { const e = knockDown(); mountTarget(e);g.tryExecution(); return e; }

test('all three maps have reachable enemies, exits and valid spawn positions', () => {
  for (let index = 0; index < g.LEVELS.length; index++) {
    g.loadLevel(index); g.updateFlow();
    const level = g.LEVELS[index];
    assert.equal(g.solid(...level.spawn, 14), false, 'spawn ' + index);
    for (const target of [...g.enemies, { x: level.exit[0], y: level.exit[1] }]) {
      assert.equal(g.solid(target.x, target.y, target.r || 10), false, `target inside obstacle ${index}: ${target.x},${target.y}`);
      const cell = Math.floor(target.y / 28) * 40 + Math.floor(target.x / 28);
      assert.ok(g.flow[cell] >= 0, `unreachable target in chapter ${index + 1}: ${target.x},${target.y}`);
    }
  }
});
test('movement cannot tunnel through walls at dash speed', () => {
  reset(); g.player.x = 430; g.player.y = 100; g.moveBody(g.player, 180, 0);
  assert.ok(g.player.x <= 436); assert.equal(g.solid(g.player.x, g.player.y, 14), false);
  g.player.x = 1060; g.player.y = 650; g.moveBody(g.player, 100, 100);
  assert.ok(g.player.x <= 1066 && g.player.y <= 666);
});
test('melee first knocks down; explicit execution then zooms, executes and installs the limb', () => {
  reset(); g.player.hp = 1; const enemy = knockDown();
  assert.equal(g.state, 'playing'); assert.equal(enemy.downed, true); assert.equal(enemy.dead, false);
  assert.equal(g.focus, null); assert.equal(g.camera.zoom, 1); assert.equal(g.worldTimeScale(), 1);
  assert.equal(g.score, 0);assert.equal(g.tryExecution(),false);mountTarget(enemy); assert.equal(g.tryExecution(), true);
  assert.equal(g.state, 'targeting');
  advance(.5); assert.ok(g.camera.zoom > 3); assert.equal(g.worldTimeScale(), .06);
  g.chooseLimb('leftArm'); assert.equal(g.state, 'executing'); assert.equal(g.worldTimeScale(), .12);
  advance(1.4); assert.equal(enemy.dead, true); assert.equal(g.player.modules.blade, 100);
  assert.equal(g.player.hp, 2); assert.equal(g.installs, 1);
  assert.equal(g.corpses[0].severed, 'leftArm'); assert.equal(g.state, 'playing'); assert.ok(g.score > 150);
});
test('slashing cannot hit behind the player or through a wall', () => {
  reset(); openSpace(); g.player.a = 0; const e = g.enemies[0]; e.x = 490; e.y = 580;
  g.slash(); assert.equal(e.dead, false);
  g.player.cd.slash = 0; g.player.x = 430; g.player.y = 100; e.x = 490; e.y = 100;
  g.slash(); assert.equal(e.dead, false);
});
test('a second close melee blow kills a downed target and leaves a salvageable missing-limb corpse', () => {
  for(const hand of ['left','right']){
    reset();const e=knockDown();g.enemies=[e];g.player.cd.slash=0;g.player.cd.gun=0;
    if(hand==='left')g.slash();else g.fire();
    assert.equal(e.dead,true);assert.equal(g.state,'playing');assert.equal(g.focus,null);assert.equal(g.camera.zoom,1);
    const body=g.corpses[0];assert.equal(g.corpses.length,1);assert.equal(body.x,e.x);assert.equal(body.y,e.y);
    assert.ok(body.missingParts.length>=1&&body.missingParts.length<4);assert.equal(body.harvested,undefined);assert.equal(body.gorePile,undefined);
    assert.equal(g.availableLimbs(body).length,4-body.missingParts.length);assert.ok(g.bloodStains.length>0);assert.ok(g.particles.some(p=>p.kind==='blood'));
    assert.equal(g.installs,0);assert.equal(g.looseArms.length,0);assert.equal(g.player.hp,3);const score=g.score;
    g.player.cd.slash=0;g.slash();advance(6);assert.equal(g.corpses.length,1);assert.equal(g.score,score);assert.equal(g.kills,1);
    mountTarget(body);assert.equal(g.tryExecution(),true);const gone=body.missingParts[0];g.chooseLimb(gone);assert.equal(g.state,'targeting');
    g.chooseLimb(g.availableLimbs(body)[0].id);advance(1.5);assert.equal(g.installs,1);assert.equal(g.kills,1);assert.equal(body.gorePile,true);
  }
});

test('shots, rotation and thrown arms finish prone targets with a guaranteed missing limb',()=>{
  reset();openSpace();equip('gun');g.player.a=0;const e=g.enemies[0];Object.assign(e,{x:630,y:580,hp:3,type:'warden'});g.downEnemy(e);g.enemies=[e];g.fire();g.updateBullets(.1);
  assert.equal(e.dead,true);assert.equal(g.corpses.length,1);assert.ok(g.corpses[0].missingParts.length>0);assert.ok(g.availableLimbs(g.corpses[0]).length>0);assert.equal(g.installs,0);
  reset();const target=knockDown();g.enemies=[target];equip('blade');g.spinAttack();assert.equal(target.dead,true);assert.ok(g.corpses[0].missingParts.length>0);assert.equal(g.installs,0);
  for(const slot of ['blade','gun']){reset();const prone=knockDown();g.enemies=[prone];g.throwArm(slot);g.updateThrownArms(.12);assert.equal(prone.dead,true);assert.ok(g.corpses[0].missingParts.length>0);assert.equal(g.looseArms.length,1);assert.equal(g.installs,0);}
});

test('a finisher still requires a live arm, short reach, facing and an unblocked blow',()=>{
  reset();const e=knockDown();g.enemies=[e];g.player.cd.slash=0;g.player.modules.blade=0;g.slash();assert.equal(e.dead,false);
  g.player.modules.blade=100;g.player.a=Math.PI;g.slash();assert.equal(e.dead,false);
  g.player.cd.slash=0;g.player.a=0;g.player.x=490;g.slash();assert.equal(e.dead,false);
  g.player.cd.slash=0;g.player.x=430;g.player.y=100;e.x=490;e.y=100;g.slash();assert.equal(e.dead,false);assert.equal(g.corpses.length,0);
});
test('a direct blow over the prone torso connects even when its center is behind the player',()=>{
  reset();const e=knockDown();g.enemies=[e];mountTarget(e);g.player.a=0;g.player.cd.slash=0;g.slash();
  assert.equal(e.dead,true);assert.ok(g.corpses[0].missingParts.length>0);assert.equal(g.installs,0);
});

test('downed enemies stay still, cannot attack, and recover after five seconds', () => {
  reset(); const e = knockDown(); g.enemies = [e]; const x = e.x, y = e.y;
  g.updateEnemies(4.9); assert.equal(e.downed, true); assert.equal(e.dead, false);
  assert.equal(e.x, x); assert.equal(e.y, y); assert.equal(g.player.hp, 3);
  g.updateEnemies(.11); assert.equal(e.downed, false); assert.equal(e.downTimer, 0);
  assert.equal(e.dead, false); assert.ok(e.cool > 0 && e.cool <= .2); assert.equal(g.score, 0);
});
test('execution requires standing on a downed target and cannot reach through walls', () => {
  reset(); openSpace(); const e = g.enemies[0]; e.x = 609; e.y = 580;
  assert.equal(g.tryExecution(), false); assert.equal(g.beginTargeting(e), false);
  g.downEnemy(e); g.player.x = 500;
  assert.equal(g.tryExecution(), false); assert.equal(g.state, 'playing'); assert.equal(g.camera.zoom, 1);
  g.player.x = 550;assert.equal(g.tryExecution(),false);mountTarget(e); assert.equal(g.tryExecution(), true); g.cancelTargeting();
  g.player.x = 430; g.player.y = 100; e.x = 490; e.y = 100;
  assert.equal(g.tryExecution(), false); assert.equal(g.beginTargeting(e), false);
  assert.equal(g.state, 'playing');
});
test('F chooses the nearest valid downed target and recovery stays frozen while selecting', () => {
  reset(); const farther = knockDown(), nearer = g.enemies[1]; nearer.x = 584; nearer.y = 580; g.downEnemy(nearer);g.player.x=586;g.player.y=580;
  const remaining = nearer.downTimer;
  handlers.get('keydown')({ code: 'KeyF', target: { tagName: 'CANVAS' }, preventDefault: noop });
  assert.equal(g.state, 'targeting'); assert.equal(g.focus.target, nearer);
  advance(1); assert.equal(nearer.downTimer, remaining); assert.ok(farther.downTimer < remaining);
  g.cancelTargeting(); advance(.5); assert.ok(nearer.downTimer < remaining);
});
test('a downed final enemy keeps the exit locked and retry clears all knockdowns', () => {
  reset(); const e = knockDown(); g.enemies = [e]; const exit = g.LEVELS[0].exit;
  g.player.x = exit[0]; g.player.y = exit[1]; g.interact();
  assert.equal(g.levelIndex, 0); assert.equal(g.state, 'playing'); assert.equal(e.dead, false);
  g.retry(); assert.equal(g.enemies.length, 5); assert.ok(g.enemies.every(enemy => !enemy.downed && enemy.downTimer === 0));
});
test('all four choices have distinct animations and leave the correct missing body part', () => {
  const animations = new Set();
  for (const part of g.LIMBS) {
    reset(); g.player.hp = 1; const e = engage();
    animations.add(part.animation); g.chooseLimb(part.id); assert.equal(g.focus.part.id, part.id);
    const quality = g.limbQuality(e, part), profile=g.focus.profile;
    advance(profile.impact - .05); assert.equal(e.dead, false); assert.equal(g.installs, 0);
    g.draw(); advance(.1); assert.equal(e.dead, true); assert.equal(g.corpses[0].severed, part.id);
    assert.equal(g.installs, 0, 'installation happens at the end, not at impact'); g.draw();
    advance(profile.duration-g.focus.t+.12); assert.equal(g.state, 'playing'); assert.equal(g.player.modules[part.type], quality);
    assert.equal(g.player.hp, 2); assert.equal(g.installs, 1); 
    assert.ok(g.player.inv > 0); assert.equal(g.focus, null);
  }
  assert.equal(animations.size, 4);
});
test('selection slows live projectiles to six percent without freezing the world', () => {
  reset(); const e = engage(), x = e.x, y = e.y;
  g.bullets.push({ x: 800, y: 600, vx: 1000, vy: 0, enemy: true, r: 3, type: 'gun', life: 2 });
  const before = g.runTime; g.update(.5);
  assert.ok(Math.abs(g.bullets[0].x - 830) < .01); assert.equal(e.x, x); assert.equal(e.y, y);
  assert.equal(g.runTime - before, .5); assert.equal(g.state, 'targeting');
  g.hurtPlayer(); assert.equal(g.player.hp, 3);
});
test('a projectile already in flight cannot kill the locked victim before the chosen cut', () => {
  reset(); const e = engage();
  g.bullets.push({ x: e.x - 8, y: e.y, vx: 100, vy: 0, enemy: false, r: 4, life: 2, hit: new Set() });
  g.updateBullets(.1); assert.equal(e.dead, false); assert.equal(e.hp, 1);
  assert.equal(g.state, 'targeting');
});
test('cancel leaves the enemy alive and smoothly restores the wide combat camera', () => {
  reset(); const e = engage(); advance(.4); assert.ok(g.camera.zoom > 2.9);
  g.cancelTargeting(); assert.equal(g.state, 'playing'); assert.equal(e.dead, false);
  assert.equal(g.focus, null); assert.equal(g.installs, 0); assert.equal(g.score, 0);
  advance(1); assert.ok(g.camera.zoom < 1.01); assert.equal(element('executionUi').hidden, true);
});
test('pause preserves selection and in-progress execution, including its camera and timeline', () => {
  reset(); engage(); advance(.3); const zoom = g.camera.zoom;
  g.pauseGame(); const elapsed = g.focus.elapsed; g.update(1);
  assert.equal(g.state, 'paused'); assert.equal(g.camera.zoom, zoom); assert.equal(g.focus.elapsed, elapsed);
  g.resume(); assert.equal(g.state, 'targeting'); assert.equal(element('executionUi').hidden, false);
  g.chooseLimb('rightLeg'); advance(.2); const cutTime = g.focus.t;
  g.pauseGame(); g.update(2); assert.equal(g.focus.t, cutTime); assert.equal(g.installs, 0);
  g.resume(); assert.equal(g.state, 'executing'); advance(1.3);
  assert.equal(g.state, 'playing'); assert.equal(g.installs, 1);
});
test('repeated confirmation is ignored and retry clears unfinished cinematic rewards and camera', () => {
  reset(); engage(); g.chooseLimb('rightArm'); g.chooseLimb('leftArm');
  assert.equal(g.focus.part.id, 'rightArm'); advance(.8); assert.ok(g.score > 0);
  g.retry(); assert.equal(g.state, 'playing'); assert.equal(g.focus, null); assert.equal(g.score, 0);
  assert.equal(g.camera.zoom, 1); assert.equal(g.installs, 0); assert.equal(g.player.modules.gun, 100);
  assert.equal(g.enemies.filter(e => !e.dead).length, 5); assert.equal(element('executionUi').hidden, true);
});
test('zoomed screen coordinates hit the four anatomical hotspots correctly', () => {
  reset(); engage(); advance(.5); const view = g.cameraView();
  for (const part of g.LIMBS) {
    const p = g.limbPosition(g.focus.target, part);
    const world = g.screenToWorld(view.x + p.x * view.s, view.y + p.y * view.s);
    assert.ok(Math.abs(world.x - p.x) < 1e-8 && Math.abs(world.y - p.y) < 1e-8);
    assert.equal(g.pickLimbAt(world.x, world.y).id, part.id);
  }
});
test('number-key selection dispatches the same execution as a pointer choice', () => {
  reset(); engage();
  handlers.get('keydown')({ code: 'Digit2', target: { tagName: 'CANVAS' }, preventDefault: noop });
  assert.equal(g.state, 'executing'); assert.equal(g.focus.part.id, 'rightArm');
  advance(1.4); assert.equal(g.player.modules.gun, 75);
});
test('armored guards must be weakened before entering anatomical selection', () => {
  reset(); openSpace(); g.player.a = 0; const e = g.enemies[0]; e.x = 592; e.y = 580; e.hp = 3; e.type = 'warden';
  g.slash(); assert.equal(e.hp, 2); assert.equal(g.state, 'playing');
  g.player.cd.slash = 0; g.slash(); assert.equal(e.hp, 1); assert.equal(g.state, 'playing');
  g.player.cd.slash = 0; g.slash(); assert.equal(g.state, 'playing'); assert.equal(e.downed, true);
  mountTarget(e);g.tryExecution(); assert.equal(g.state, 'targeting');
  for (const part of g.LIMBS) assert.equal(g.limbQuality(e, part), 100);
});
test('one hit shatters one randomly chosen working component with visible debris', () => {
  const previous=testMath.random;
  try { for(const [roll,id,type] of [[.05,'leftArm','blade'],[.3,'rightArm','gun'],[.6,'leftLeg','legs'],[.95,'rightLeg','legs']]){
    reset();equip('blade',100);equip('gun',100);equip('legs',100);testMath.random=()=>roll;
    g.hurtPlayer();assert.equal(g.player.lastHitPart,id);assert.equal(g.player.hp,2);
    for(const key of ['blade','gun','legs'])assert.equal(g.player.modules[key],key===type?0:100);
    assert.ok(g.particles.some(p=>p.kind==='debris'));assert.equal(g.player.modules[type],0);if(type!=='legs')assert.equal(g.player.attached[type],true);
    if(type==='gun'){const count=g.bullets.length;g.fire();assert.equal(g.bullets.length,count);}
    if(type==='blade'){const before=g.player.cd.spin;g.spinAttack();assert.equal(g.player.cd.spin,before);}
  }}finally{testMath.random=previous;}
});
test('invulnerability prevents repeat damage; death exposes a restart state', () => {
  reset(); g.hurtPlayer(); g.hurtPlayer(); assert.equal(g.player.hp, 2);
  g.player.inv = 0; g.hurtPlayer(); g.player.inv = 0; g.hurtPlayer();
  assert.equal(g.state, 'dead'); assert.equal(g.player.hp, 0);
});
test('equipped rotation hits around the player and consumes durability', () => {
  reset(); openSpace(); equip('blade',100);
  g.enemies[0].x = 495; g.enemies[0].y = 580;
  g.spinAttack(); assert.equal(g.enemies[0].dead, true); assert.equal(g.player.modules.blade, 84);
  assert.ok(g.player.cd.spin > 2); assert.ok(g.player.inv > 0);
});
test('pulse projectiles damage enemies and do not pass through walls', () => {
  reset(); openSpace(); equip('gun',100); g.player.a = 0;
  const e = g.enemies[0]; e.x = 630; e.y = 580; g.fire();
  for (let i = 0; i < 20; i++) g.updateBullets(1 / 60);
  assert.equal(e.dead, true); assert.equal(g.player.modules.gun, 93);
  reset(); g.player.x = 410; g.player.y = 100; g.player.a = 0; equip('gun',100);
  g.enemies[0].x = 500; g.enemies[0].y = 100; g.fire();
  for (let i = 0; i < 20; i++) g.updateBullets(1 / 60);
  assert.equal(g.enemies[0].dead, false); assert.equal(g.bullets.length, 0);
});
test('enhanced dodge moves, damages on contact and consumes leg durability', () => {
  reset(); openSpace(500, 600); equip('legs',100); g.player.a = 0;
  g.enemies[0].x = 560; g.enemies[0].y = 600; g.keys.add('KeyD'); g.dodge();
  for (let i = 0; i < 20; i++) g.update(1 / 60); g.keys.clear();
  assert.ok(g.player.x > 580); assert.equal(g.enemies[0].dead, true);
  assert.equal(g.player.modules.legs, 91); assert.equal(g.player.hp, 3);
});
test('retry restores chapter checkpoint score, time and equipment', () => {
  reset(); g.player.modules.gun = 87; g.score = 1000; g.runTime = 10; g.nextLevel();
  assert.equal(g.levelIndex, 1); assert.equal(g.state, 'briefing');
  g.state = 'playing'; g.player.modules.gun = 10; g.score = 2000; g.runTime = 40;
  g.retry(); assert.equal(g.player.modules.gun, 87); assert.equal(g.score, 1000);
  assert.equal(g.runTime, 10); assert.equal(g.state, 'playing'); assert.equal(g.player.hp, 3);
});
test('normal deaths leave corpses and blood without automatically granting or dropping parts', () => {
  reset(); openSpace(); g.player.hp=1; g.player.modules.gun=70;
  const e=g.enemies[0];e.x=609;e.y=580;g.killEnemy(e,'ranged');
  assert.equal(g.corpses.length,1);assert.equal(g.corpses[0].severed,null);assert.ok(g.bloodStains.length>0);
  assert.equal(g.installs,0);assert.equal(g.looseArms.length,0);g.player.x=e.x;g.player.y=e.y;g.interact();
  assert.equal(g.player.modules.gun,70);assert.equal(g.player.modules.blade,100);assert.equal(g.player.modules.legs,100);
  assert.equal(g.player.hp,1);assert.equal(g.installs,0);
  assert.ok(!source.includes('drops.push'), 'only a confirmed execution harvests a limb');
});
test('the execution victim lies face-up with separated limb targets and an unobstructed torso', () => {
  reset(); const e=engage(); assert.equal(e.proneAngle,0);
  const points=g.LIMBS.map(part=>g.limbPosition(e,part));
  for(let i=0;i<points.length;i++)for(let j=i+1;j<points.length;j++){
    assert.ok(Math.hypot(points[i].x-points[j].x,points[i].y-points[j].y)>22);
  }
  assert.equal(g.focus.observe.x,g.player.x);assert.equal(g.focus.observe.y,g.player.y);assert.equal(g.isOnExecutionBody(e),true);
  assert.equal(g.focus.phase,'select');g.draw();
});
test('ripping has a grip-and-pull phase before detachment, followed by blood and installation', () => {
  reset(); const e=engage(); g.enemies=[e];g.chooseLimb('leftArm');advance(.21);
  assert.equal(g.focus.gripped,true);assert.equal(g.focus.impacted,false);assert.equal(e.dead,false);
  assert.equal(g.bloodStains.length,0);assert.ok(element('executionStep').textContent.includes('扯'));
  advance(g.focus.profile.impact-g.focus.t+.03);
  assert.equal(e.dead,true);assert.ok(g.particles.some(p=>p.kind==='blood'));assert.ok(g.bloodStains.length>0);
  assert.equal(g.corpses[0].severed,'leftArm');assert.equal(g.installs,0);g.draw();
  advance(g.focus.profile.duration-g.focus.t+.12);assert.equal(g.installs,1);assert.equal(g.state,'playing');
});
test('corpses keep their original position and missing limb; blood persists without despawning', () => {
  reset(); const e=engage();g.enemies=[e];const x=e.x,y=e.y;
  g.chooseLimb('rightLeg');advance(1.6);
  assert.equal(g.corpses.length,1);assert.equal(g.corpses[0].x,x);assert.equal(g.corpses[0].y,y);
  assert.equal(g.corpses[0].severed,'rightLeg');const body=JSON.stringify(g.corpses[0]),stains=g.bloodStains.length;
  advance(30);assert.equal(g.corpses.length,1);assert.equal(JSON.stringify(g.corpses[0]),body);
  assert.ok(g.bloodStains.length>=stains);assert.ok(!g.particles.some(p=>p.kind==='blood'));g.draw();
  g.loadLevel(1);assert.equal(g.corpses.length,0);assert.equal(g.bloodStains.length,0);
});
test('locked exits block advancement; clearing all three chapters reaches the ending', () => {
  reset(); const ex = g.LEVELS[0].exit; g.player.x = ex[0]; g.player.y = ex[1]; g.interact();
  assert.equal(g.levelIndex, 0);
  for (let i = 0; i < 3; i++) {
    g.state = 'playing'; for (const e of g.enemies) g.killEnemy(e);
    const exit = g.LEVELS[i].exit; g.player.x = exit[0]; g.player.y = exit[1]; g.interact();
    assert.equal(g.state, i === 2 ? 'won' : 'briefing');
  }
  assert.ok(g.score > 4000); assert.ok(Number(storage.get('rebody-best')) > 0);
});
test('narrative chips and journal interactions are absent from the delivered game', () => {
  assert.ok(!html.includes('journalBtn'));assert.ok(!html.includes('chipCount'));assert.ok(!source.includes('showJournal'));
  assert.ok(g.LEVELS.every(level=>!('chip' in level)));
});
test('pause freezes combat timer; render works in every chapter', () => {
  reset(); g.pauseGame(); const before = g.runTime; g.update(1); assert.equal(g.runTime, before);
  for (let i = 0; i < 3; i++) { g.loadLevel(i); g.draw(); }
});

test('broken arms cannot attack, while an intact opposite hand still punches',()=>{
  reset();openSpace();g.player.a=0;const e=g.enemies[0];e.x=592;e.y=580;g.enemies=[e];
  g.player.modules.blade=0;g.slash();assert.equal(e.downed,false);assert.equal(g.player.cd.slash,0);
  g.fire();assert.equal(e.downed,true);assert.equal(g.player.attackingHand,'gun');
  reset();equip('gun',0);const n=g.bullets.length;g.fire();assert.equal(g.bullets.length,n);assert.equal(g.player.cd.gun,0);
});
test('each arm detaches independently, knocks down, and can be recovered without a healing or score exploit',()=>{
  for(const slot of ['blade','gun']){
    reset();openSpace();g.player.hp=1;g.player.a=0;equip(slot);const e=g.enemies[0];e.x=609;e.y=580;g.enemies=[e];
    const opposite=slot==='blade'?'gun':'blade';g.throwArm(slot);
    assert.equal(g.player.attached[slot],false);assert.equal(g.player.modules[slot],0);assert.equal(g.player.attached[opposite],true);
    assert.equal(g.thrownArms.length,1);g.draw();g.updateThrownArms(.12);
    assert.equal(e.downed,true);assert.equal(e.dead,false);assert.equal(g.thrownArms.length,0);assert.equal(g.looseArms.length,1);
    const score=g.score;g.interact();assert.equal(g.player.attached[slot],true);assert.equal(g.player.modules[slot],95);
    assert.equal(g.player.componentKinds[slot],'salvaged');assert.equal(g.player.hp,1);assert.equal(g.score,score);assert.equal(g.installs,0);assert.equal(g.looseArms.length,0);
  }
});
test('a broken thrown arm explodes on standing or downed enemies and directly kills armored targets',()=>{
  for(const downed of [false,true]){
    reset();openSpace();g.player.a=0;equip('blade',0);const e=g.enemies[0];e.x=609;e.y=580;e.hp=3;e.type='warden';e.downed=downed;g.enemies=[e];
    g.throwArm('blade');assert.equal(g.thrownArms[0].explosive,true);g.draw();g.updateThrownArms(.15);
    assert.equal(e.dead,true);assert.equal(g.kills,1);assert.equal(g.corpses.length,1);assert.equal(g.thrownArms.length,0);assert.equal(g.looseArms.length,0);if(downed)assert.ok(g.corpses[0].missingParts.length>0);
    assert.ok(g.particles.some(p=>p.kind==='blood'));assert.ok(g.bloodStains.length>0);assert.equal(g.installs,0);g.draw();
  }
});
test('leg failure reduces travel speed and melee reach and prevents dodge',()=>{
  reset();openSpace();g.enemies=[];g.keys.add('KeyD');g.update(.1);const standing=g.player.x-550;g.keys.clear();
  reset();openSpace();g.enemies=[];g.player.modules.legs=0;g.keys.add('KeyD');g.update(.1);g.keys.clear();
  assert.ok(Math.abs(standing-30)<.01);assert.ok(Math.abs(g.player.x-550-10.5)<.01);g.dodge();assert.equal(g.player.dash,0);g.draw();
  for(const working of [false,true]){
    reset();openSpace();g.player.a=0;g.player.modules.legs=working?100:0;const e=g.enemies[0];e.x=600;e.y=580;g.enemies=[e];
    g.slash();assert.equal(e.downed,working);
  }
});
test('each corpse can be harvested once, becomes persistent gore, and grants only one chosen component',()=>{
  const previous=testMath.random;try{
    testMath.random=()=>.9;reset();openSpace();const e=g.enemies[0];e.x=609;e.y=580;g.enemies=[e];g.killEnemy(e,'ranged');const score=g.score;mountTarget(g.corpses[0]);
    assert.equal(g.tryExecution(),true);g.chooseLimb('rightArm');advance(.9);const body=g.corpses[0];
    assert.equal(g.state,'playing');assert.equal(body.gorePile,true);assert.equal(body.harvested,true);assert.ok(body.fragments.length>5);assert.equal(g.corpses.length,1);assert.equal(g.kills,1);
    assert.equal(g.score,score+75);assert.equal(g.installs,1);assert.equal(g.player.modules.gun,75);assert.equal(g.player.componentKinds.blade,'standard');
    assert.equal(g.availableLimbs(body).length,0);assert.equal(g.tryExecution(),false);assert.equal(g.beginTargeting(body),false);assert.equal(g.executionTarget(),null);
    const remains=JSON.stringify(body);advance(20);assert.equal(JSON.stringify(body),remains);assert.equal(body.x,609);assert.equal(body.y,580);g.draw();
  }finally{testMath.random=previous;}
});
test('battle-damaged corpses expose only surviving selectable limbs',()=>{
  const previous=testMath.random;try{
    testMath.random=()=>.1;reset();openSpace();const e=g.enemies[0];e.x=609;e.y=580;g.enemies=[e];g.killEnemy(e,'ranged');
    const body=g.corpses[0];mountTarget(body);assert.equal(g.missingParts(body).length,3);assert.equal(g.availableLimbs(body)[0].id,'rightLeg');
    assert.equal(g.tryExecution(),true);assert.equal(element('chooseLeftArm').disabled,true);assert.equal(element('chooseRightLeg').disabled,false);
    const p=g.limbPosition(body,g.LIMBS[0]);assert.equal(g.pickLimbAt(p.x,p.y),null);g.chooseLimb('leftArm');assert.equal(g.state,'targeting');
    g.draw();g.chooseLimb('rightLeg');advance(.85);assert.equal(g.availableLimbs(body).length,0);assert.equal(g.installs,1);
  }finally{testMath.random=previous;}
});
test('the four enemy roles keep distinct compact prone silhouettes with no death scale jump',()=>{
  reset();const signatures=new Set();
  for(const [type,style]of Object.entries(g.BODY_STYLES)){
    const e={...g.enemies[0],x:550,y:580,type,proneAngle:0};signatures.add(JSON.stringify(style));
    for(const part of g.LIMBS){const p=g.localLimb(e,part);assert.ok(Math.abs(p.x)<=28);assert.ok(Math.abs(p.y)<=28);}
    g.drawProneActor(context2d,e);g.drawProneActor(context2d,{...e,isCorpse:true,dead:true,missingParts:['rightArm']},{corpse:true});
  }
  assert.equal(signatures.size,4);
});
test('chapter transition and retry preserve thrown-off arms rather than regrowing them',()=>{
  reset();g.throwArm('blade');equip('gun',40);g.nextLevel();assert.equal(g.player.attached.blade,false);assert.equal(g.player.modules.blade,0);
  assert.equal(g.player.componentKinds.gun,'salvaged');assert.equal(g.player.modules.gun,40);g.state='playing';g.player.attached.blade=true;g.player.modules.blade=90;
  g.retry();assert.equal(g.player.attached.blade,false);assert.equal(g.player.modules.blade,0);assert.equal(g.player.modules.gun,40);
});


test('short melee connects only at close range for normal, enhanced and crawling attacks',()=>{
  for(const [legs,blade,hitDistance,missDistance]of [[true,false,50,58],[true,true,68,76],[false,false,35,43],[false,true,45,51]]){
    for(const [d,hit]of [[hitDistance,true],[missDistance,false]]){
      reset();openSpace();g.player.a=0;g.player.modules.legs=legs?100:0;if(blade)equip('blade');const e=g.enemies[0];g.enemies=[e];e.x=550+d;e.y=580;
      g.slash();assert.equal(e.downed,hit,JSON.stringify({legs,blade,d}));g.draw();
    }
  }
  reset();openSpace();g.player.a=0;const e=g.enemies[0];g.enemies=[e];e.x=550+44*Math.cos(1.1);e.y=580+44*Math.sin(1.1);g.slash();assert.equal(e.downed,false,'attack cone must face the target');
});
test('incoming hits skip missing and already broken components and do not cause an enemy-killing blast',()=>{
  const previous=testMath.random;try{testMath.random=()=>.01;reset();openSpace();g.player.modules.blade=0;g.player.attached.blade=false;g.player.modules.gun=0;
    const e=g.enemies[0];e.x=580;e.y=580;g.hurtPlayer();assert.equal(g.player.lastHitPart,'leftLeg');assert.equal(g.player.modules.legs,0);assert.equal(g.player.hp,2);assert.equal(e.dead,false);
    const fragments=g.particles.filter(p=>p.kind==='debris').length;g.player.inv=0;g.hurtPlayer();assert.equal(g.player.hp,1);assert.equal(g.player.lastHitPart,null);assert.equal(g.particles.filter(p=>p.kind==='debris').length,fragments);
  }finally{testMath.random=previous;}
});
test('mounted execution settles directly over the victim and keeps the corpse in place',()=>{
  reset();const e=engage();g.enemies=[e];const x=e.x,y=e.y;g.chooseLimb('leftArm');assert.equal(g.focus.profile.mode,'mounted');
  advance(.24);const pose=g.executionPose(g.focus);assert.equal(pose.grounded,false);assert.ok(Math.hypot(pose.x-e.x,pose.y-e.y)<15);assert.equal(g.focus.mounted,true);assert.equal(g.focus.gripped,true);assert.equal(e.dead,false);g.draw();
  const profile=g.focus.profile;advance(profile.duration-g.focus.t+.15);assert.equal(g.state,'playing');assert.equal(g.player.x,g.corpses[0].x);assert.ok(Math.abs(g.player.y-g.corpses[0].y)<=16);assert.equal(g.corpses[0].x,x);assert.equal(g.corpses[0].y,y);
});
test('legless executions have a slower grounded motion and recover legs only after finishing',()=>{
  reset();const e=knockDown();g.enemies=[e];g.player.modules.legs=0;mountTarget(e);assert.equal(g.tryExecution(),true);g.chooseLimb('leftLeg');
  const f=g.focus,p=f.profile,standing=g.executionProfile(f.part,true);assert.equal(p.mode,'grounded');assert.ok(p.duration>standing.duration*1.5);assert.ok(p.impact>standing.impact);
  advance(.4);const pose=g.executionPose(f);assert.equal(pose.grounded,true);assert.ok(pose.sy<.6);assert.ok(Math.hypot(pose.x-e.x,pose.y-e.y)<22);assert.equal(e.dead,false);assert.equal(g.player.modules.legs,0);g.draw();
  g.pauseGame();const t=f.t;advance(.5);assert.equal(f.t,t);g.resume();advance(p.impact-f.t+.06);assert.equal(e.dead,true);assert.equal(g.player.modules.legs,0);assert.equal(g.installs,0);g.draw();
  advance(p.duration-f.t+.15);assert.equal(g.state,'playing');assert.equal(g.player.modules.legs,75);assert.equal(g.installs,1);
});
test('mounting beside a boundary selects a collision-free position without shifting the victim',()=>{
  reset();openSpace(592,654);const e=g.enemies[0];e.x=592;e.y=662;g.enemies=[e];g.downEnemy(e);assert.equal(g.tryExecution(),true);g.chooseLimb('rightArm');
  assert.equal(g.solid(g.focus.mount.x,g.focus.mount.y,g.player.r),false);assert.equal(g.focus.mount.x,e.x);assert.equal(g.focus.mount.y,e.y);advance(1);assert.equal(g.corpses[0].y,662);assert.equal(g.solid(g.player.x,g.player.y,g.player.r),false);
});


test('a live victim becomes unselectable gore after its first execution as well',()=>{
  reset();const e=engage();g.enemies=[e];g.chooseLimb('leftArm');advance(.9);const body=g.corpses[0];
  assert.equal(body.harvested,true);assert.equal(body.gorePile,true);assert.equal(g.tryExecution(),false);assert.equal(g.beginTargeting(body),false);assert.equal(g.kills,1);assert.equal(g.installs,1);
  const score=g.score;g.chooseLimb('rightArm');g.finishExecution();assert.equal(g.score,score);assert.equal(g.installs,1);g.draw();
});


test('being near a body is insufficient: F requires the torso footprint for both downed enemies and untouched corpses',()=>{
  for(const corpse of [false,true]){
    reset();const e=knockDown();g.enemies=[e];if(corpse)g.killEnemy(e,'ranged');const target=corpse?g.corpses[0]:e;
    for(const [x,y,allowed]of [[22,0,false],[0,-20,false],[15,23,true]]){
      const p=g.bodyPoint(target,x,y);g.player.x=p.x;g.player.y=p.y;
      assert.equal(g.isOnExecutionBody(target),allowed);assert.equal(g.executionTarget()===target,allowed);
      if(!allowed)assert.equal(g.beginTargeting(target),false);
    }
    assert.equal(g.tryExecution(),true);assert.equal(g.focus.observe.x,g.player.x);assert.equal(g.focus.observe.y,g.player.y);g.draw();
  }
});


test('Q and E throw independent arms, while Z spins and X recovers without input conflicts',()=>{
  const press=code=>{handlers.get('keydown')({code,target:{tagName:'CANVAS'},preventDefault:noop});handlers.get('keyup')({code});};
  reset();openSpace();g.enemies=[];press('KeyQ');assert.equal(g.player.attached.blade,false);assert.equal(g.player.attached.gun,true);assert.equal(g.player.cd.spin,0);assert.equal(g.thrownArms[0].slot,'blade');
  press('KeyE');assert.equal(g.player.attached.gun,false);assert.equal(g.thrownArms.length,2);assert.equal(g.thrownArms[1].slot,'gun');
  g.updateThrownArms(.8);assert.equal(g.looseArms.length,2);g.player.hp=1;press('KeyX');press('KeyX');assert.equal(g.player.attached.blade,true);assert.equal(g.player.attached.gun,true);assert.equal(g.looseArms.length,0);assert.equal(g.player.hp,1);
  reset();equip('blade');press('KeyZ');assert.ok(g.player.spin>0);assert.equal(g.player.attached.blade,true);assert.equal(g.thrownArms.length,0);
});


test('the unmodified opening encounter punishes standing still in under two seconds',()=>{
  g.beginRun();g.keys.clear();g.mouse.active=false;let firstHit=0;
  for(let i=0;i<180&&g.state==='playing';i++){g.update(1/60);if(!firstHit&&g.player.hp<3)firstHit=g.runTime;}
  assert.equal(g.state,'dead');assert.ok(firstHit>.35&&firstHit<1);assert.ok(g.runTime<1.8);assert.equal(g.player.hp,0);
  console.log('  Opening idle: first hit '+firstHit.toFixed(2)+'s, death '+g.runTime.toFixed(2)+'s');
});
test('gunfire is quickly lethal, while cover and a timed dodge still stop it',()=>{
  reset();openSpace();const e=g.enemies[0];Object.assign(e,{x:630,y:580,type:'gun',cool:0,alert:false});g.enemies=[e];advance(.2);assert.equal(g.player.hp,3);advance(.4);assert.equal(g.state,'dead');assert.equal(g.player.hp,0);
  reset();openSpace(500,100);g.enemies=[];g.bullets.push({x:430,y:100,vx:1000,vy:0,life:1,enemy:true,r:4,damage:3});g.updateBullets(.2);assert.equal(g.player.hp,3);assert.equal(g.bullets.length,0,'cover blocks fast rounds');
  reset();openSpace();g.enemies=[];g.player.dash=.12;g.bullets.push({x:510,y:580,vx:1000,vy:0,life:1,enemy:true,r:4,damage:3});g.updateBullets(.1);assert.equal(g.player.hp,3,'dodge passes through an otherwise fatal round');
});
test('two close melee strikes kill rapidly, but sidestepping a committed lunge avoids the hit',()=>{
  reset();openSpace();const e=g.enemies[0];Object.assign(e,{x:600,y:580,type:'blade',cool:0,alert:true});g.enemies=[e];g.updateEnemies(.001);g.updateEnemies(.16);assert.equal(g.player.hp,1);assert.ok(Object.values(g.player.modules).includes(0));advance(.85);assert.equal(g.state,'dead');
  reset();openSpace();const attacker=g.enemies[0];Object.assign(attacker,{x:600,y:580,type:'blade',cool:0,alert:true});g.enemies=[attacker];g.updateEnemies(.001);g.player.y=510;g.updateEnemies(.16);assert.equal(g.player.hp,3,'the attack cannot turn after committing');
});
test('shooters lead movement then commit their aim before firing rather than homing at the last instant',()=>{
  reset();openSpace();const e=g.enemies[0];Object.assign(e,{x:700,y:580,type:'gun',cool:0,alert:true});g.enemies=[e];g.player.moveY=-1;g.updateEnemies(.001);assert.ok(Math.sin(e.attackA)<-.05,'aim leads the current movement');
  g.player.y=600;g.player.moveY=1;g.updateEnemies(.13);const locked=e.attackA;g.player.y=500;g.player.moveY=-1;g.updateEnemies(.04);assert.equal(e.attackA,locked);g.updateEnemies(.04);assert.equal(g.bullets.length,1);const b=g.bullets[0];assert.ok(Math.abs(Math.atan2(b.vy,b.vx)-locked)<1e-8);assert.ok(Math.hypot(b.vx,b.vy)>900);
});
test('a loud shot draws enemies from behind cover without letting them attack through the wall',()=>{
  reset();openSpace(500,100);equip('gun');const e=g.enemies[0];Object.assign(e,{x:430,y:100,type:'blade',cool:0,alert:false});g.enemies=[e];g.updateFlow();assert.equal(g.visible(e.x,e.y,g.player.x,g.player.y),false);g.fire();assert.equal(e.alert,true);const y=e.y;g.updateEnemies(.1);assert.ok(e.y!==y||e.x!==430);assert.equal(e.wind,0);assert.equal(g.player.hp,3);
});
test('runners close the gap on a normally running player instead of being kited forever',()=>{
  reset();openSpace(500,600);const e=g.enemies[0];Object.assign(e,{x:430,y:600,type:'legs',cool:100,alert:true});g.enemies=[e];g.keys.add('KeyD');advance(.4);g.keys.clear();assert.ok(g.player.x>610);assert.ok(Math.hypot(g.player.x-e.x,g.player.y-e.y)<60);assert.equal(g.player.hp,3);
});

assert.ok(!/<(?:script|link|img|audio)[^>]+(?:src|href)=["']https?:/i.test(html), 'standalone assets');
console.log(`\n${passed} gameplay checks passed. No external runtime assets.`);
