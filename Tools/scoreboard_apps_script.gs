/** @OnlyCurrentDoc */
// ↑ 이 줄이 있으면 권한이 "이 스프레드시트 하나"로 제한된다 (없으면 내 드라이브의 모든 스프레드시트 권한을 요청함). 지우지 말 것.

/**
 * 마우스코드 온라인 스코어보드 — 구글 시트 + Apps Script 웹 앱
 *
 * ── 설치 (한 번만) ──
 * 1. 구글 드라이브에서 새 스프레드시트를 만든다 (이름 자유, 예: "마우스코드 스코어보드").
 * 2. 메뉴 [확장 프로그램] → [Apps Script]를 연다.
 * 3. 기본으로 들어 있는 코드를 전부 지우고 이 파일 내용을 통째로 붙여 넣은 뒤 저장(Ctrl+S).
 * 4. 오른쪽 위 [배포] → [새 배포] → 톱니바퀴에서 유형 [웹 앱] 선택
 *      - 설명: 아무거나
 *      - 다음 사용자 인증 정보로 실행: [나]
 *      - 액세스 권한이 있는 사용자: [모든 사용자]   ← 꼭 이걸로 (로그인 없이 게임이 접속해야 함)
 *    [배포]를 누르고 권한 승인 창이 뜨면 본인 계정으로 허용한다.
 *    ("Google에서 확인하지 않은 앱" 경고는 개인 스크립트면 항상 나온다. 개발자가 본인이라 괜찮다.
 *     [고급] → [(프로젝트 이름)(으)로 이동(안전하지 않음)] → 요청 권한이
 *     "이 애플리케이션이 설치된 스프레드시트 보기 및 관리" 하나뿐인지 확인하고 [허용].)
 * 5. 나온 "웹 앱 URL"(https://script.google.com/macros/s/……/exec)을 복사해서
 *    Unity의 Assets/MouthCode/Resources/GameConfig 에셋 → scoreboardUrl 칸에 붙여 넣고 다시 빌드한다.
 *
 * ── 코드를 고쳤을 때 (URL 유지) ──
 * [배포] → [배포 관리] → 연필 아이콘 → 버전: [새 버전] → [배포].
 * ([새 배포]를 누르면 URL이 바뀌니 주의. 게임에 박힌 URL을 다시 넣고 빌드해야 한다.)
 *
 * ── 쓰는 법 ──
 * - 기록은 시트의 "scores" 탭에 한 줄씩 쌓인다. 장난 기록은 그 줄을 지우면 끝.
 * - 브라우저로 웹 앱 URL을 열면 현재 순위가 JSON으로 보인다 (동작 확인용).
 * - 게임은 기록을 GET ?submit=<기록 JSON> 으로 보낸다 (Apps Script의 POST 리다이렉트 문제를 피하려고). POST도 받는다.
 *
 * ── 장난 방지 (인증이 없으니 완벽하진 않다. 남는 건 시트에서 지운다) ──
 * - 말이 안 되는 기록(점수가 그 날짜까지 낼 수 있는 최대치보다 큼, 날짜 범위 밖, id 형식 이상)은
 *   조용히 버린다. 보낸 쪽에는 성공처럼 보이게 해서 다시 보내지 않게 한다.
 * - 1분에 RATE_PER_MIN개보다 많이 들어오면 잠시 거절한다 (게임은 다음 접속 때 다시 보낸다).
 * - 시트 기록이 MAX_ROWS개를 넘으면 새 기록을 받지 않는다 (도배당했을 때 시트가 무거워지지 않게).
 * - 오류 내용은 밖으로 내보내지 않는다 (자세한 내용은 Apps Script [실행] 기록에서만 보임).
 */

var SHEET_NAME = 'scores';
var HEADER = ['id', 'name', 'day', 'score', 'accuracy', 'hard', 'rank', 'date', 'received'];
var MAX_LIMIT = 50;
var MAX_DAY = 200;        // 실제 플레이로는 이만큼 못 간다
var MAX_ROWS = 3000;      // 시트에 쌓아 둘 최대 기록 수
var RATE_PER_MIN = 20;    // 전체 합쳐 1분에 받는 기록 수

/**
 * 그 날짜까지 낼 수 있는 최대 점수 (게임 점수 공식이 바뀌면 같이 고칠 것).
 * 송신 1건 최대 = 100% × 10점 × day, 연속 완벽 보너스 최대 +100% → 2000 × day.
 * 하루 송신은 최대 6건. 1일차부터 day일차까지 전부 만점이라고 쳐서 더한다.
 */
function maxScore_(day) {
  return 6000 * day * (day + 1);
}

function sheet_() {
  var ss = SpreadsheetApp.getActiveSpreadsheet();
  var sh = ss.getSheetByName(SHEET_NAME);
  if (!sh) {
    sh = ss.insertSheet(SHEET_NAME);
    sh.appendRow(HEADER);
    sh.setFrozenRows(1);
  }
  return sh;
}

/** 시트의 기록 전체를 점수 순으로 */
function sorted_() {
  var values = sheet_().getDataRange().getValues();
  values.shift(); // 머리줄
  var rows = [];
  for (var i = 0; i < values.length; i++) {
    var r = values[i];
    if (r[0] === '' && r[1] === '') continue;
    rows.push({
      id: String(r[0]),
      name: String(r[1]),
      day: Number(r[2]) || 0,
      score: Number(r[3]) || 0,
      accuracy: Number(r[4]) || 0,
      hard: r[5] === true || String(r[5]).toUpperCase() === 'TRUE',
      rank: String(r[6]),
      date: String(r[7])
    });
  }
  rows.sort(function (a, b) { return (b.score - a.score) || (b.day - a.day); });
  return rows;
}

function reply_(limit, id) {
  var rows = sorted_();
  var place = 0;
  if (id) {
    for (var i = 0; i < rows.length; i++) {
      if (rows[i].id === id) { place = i + 1; break; }
    }
  }
  var out = { ok: true, total: rows.length, myPlace: place, entries: rows.slice(0, limit) };
  return ContentService.createTextOutput(JSON.stringify(out)).setMimeType(ContentService.MimeType.JSON);
}

/** 게임에 보여 줄 짧은 거절 사유. 내부 오류 내용은 넣지 않는다. */
function fail_(message) {
  var out = { ok: false, error: String(message), entries: [] };
  return ContentService.createTextOutput(JSON.stringify(out)).setMimeType(ContentService.MimeType.JSON);
}

/** 예상 못 한 오류: 자세한 건 실행 기록에만 남기고 밖에는 한 줄만 */
function crash_(err) {
  console.error(err && err.stack ? err.stack : err);
  return fail_('서버 오류');
}

function limit_(e) {
  var n = Number(e && e.parameter && e.parameter.limit) || 10;
  return Math.max(1, Math.min(MAX_LIMIT, Math.floor(n)));
}

/** 글자 정리: 길이 제한 + 시트 수식으로 해석되지 않게 */
function text_(v, maxLen) {
  var s = String(v == null ? '' : v).replace(/[\u0000-\u001f\u007f]/g, ' ').trim().slice(0, maxLen);
  return /^[=+\-@]/.test(s) ? "'" + s : s;
}

/** 순위 조회: GET ?limit=10[&id=기록id]   기록 추가: GET ?submit=<기록 JSON> */
function doGet(e) {
  var p = (e && e.parameter) || {};
  if (p.submit) {
    var d;
    try { d = JSON.parse(p.submit); } catch (err) { return fail_('형식 오류'); }
    return add_(e, d);
  }
  try {
    return reply_(limit_(e), String(p.id || '').slice(0, 32));
  } catch (err) {
    return crash_(err);
  }
}

/** 기록 추가: POST 본문 = 기록 JSON */
function doPost(e) {
  var d;
  try { d = JSON.parse(e.postData.contents); } catch (err) { return fail_('형식 오류'); }
  return add_(e, d);
}

/** 1분 단위 전체 접수량. 넘치면 false (잠금 안에서만 부른다) */
function takeRate_() {
  var cache = CacheService.getScriptCache();
  var key = 'rate:' + Math.floor(Date.now() / 60000);
  var used = Number(cache.get(key)) || 0;
  if (used >= RATE_PER_MIN) return false;
  cache.put(key, String(used + 1), 120);
  return true;
}

/** 기록 한 줄 추가 후 순위를 돌려준다 */
function add_(e, d) {
  if (!d || typeof d !== 'object' || Array.isArray(d)) return fail_('형식 오류');

  var id = String(d.id == null ? '' : d.id);
  var day = Math.floor(Number(d.day));
  var score = Math.floor(Number(d.score));
  var accuracy = Number(d.accuracy);
  var plausible =
    /^[0-9A-Za-z]{8,32}$/.test(id) &&
    isFinite(day) && day >= 1 && day <= MAX_DAY &&
    isFinite(score) && score >= 0 && score <= maxScore_(day) &&
    isFinite(accuracy) && accuracy >= 0 && accuracy <= 1;
  // 말이 안 되는 기록은 넣지 않지만 성공처럼 답한다 (게임 쪽이 같은 걸 계속 재전송하지 않게)
  if (!plausible) {
    try { return reply_(limit_(e), ''); } catch (err) { return crash_(err); }
  }

  var lock = LockService.getScriptLock();
  try {
    lock.waitLock(10000);
    var sh = sheet_();
    var last = sh.getLastRow();
    // 같은 기록이 두 번 오면(재전송) 한 번만 넣는다
    var ids = sh.getRange(1, 1, Math.max(1, last), 1).getValues();
    var exists = ids.some(function (r) { return String(r[0]) === id; });
    if (!exists) {
      if (last - 1 >= MAX_ROWS) return fail_('기록 칸이 가득 참');
      if (!takeRate_()) return fail_('잠시 후 다시');
      sh.appendRow([
        id,
        text_(d.name, 12) || '무명 통신병',
        day,
        score,
        accuracy,
        d.hard === true,
        text_(d.rank, 16),
        text_(d.date, 20),
        new Date()
      ]);
    }
    return reply_(limit_(e), id);
  } catch (err) {
    return crash_(err);
  } finally {
    lock.releaseLock();
  }
}
