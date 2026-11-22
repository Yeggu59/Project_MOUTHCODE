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
 * ── 쓰는 법 ──
 * - 기록은 시트의 "scores" 탭에 한 줄씩 쌓인다. 장난 기록은 그 줄을 지우면 끝.
 * - 코드를 고쳤다면 [배포] → [배포 관리] → 연필 → 버전 [새 버전] → [배포] (URL은 그대로 유지됨).
 * - 브라우저로 웹 앱 URL을 열면 현재 순위가 JSON으로 보인다 (동작 확인용).
 * - 게임은 기록을 GET ?submit=<기록 JSON> 으로 보낸다 (Apps Script의 POST 리다이렉트 문제를 피하려고). POST도 받는다.
 *
 * ── 주의 ──
 * - URL을 아는 사람은 누구나 기록을 보낼 수 있다 (게임잼 용도라 인증 없음). 이상한 기록은 시트에서 지운다.
 */

var SHEET_NAME = 'scores';
var HEADER = ['id', 'name', 'day', 'score', 'accuracy', 'hard', 'rank', 'date', 'received'];
var MAX_LIMIT = 50;

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

function fail_(message) {
  var out = { ok: false, error: String(message), entries: [] };
  return ContentService.createTextOutput(JSON.stringify(out)).setMimeType(ContentService.MimeType.JSON);
}

function limit_(e) {
  var n = Number(e && e.parameter && e.parameter.limit) || 10;
  return Math.max(1, Math.min(MAX_LIMIT, Math.floor(n)));
}

/** 글자 정리: 길이 제한 + 시트 수식으로 해석되지 않게 */
function text_(v, maxLen) {
  var s = String(v == null ? '' : v).replace(/[\r\n\t]/g, ' ').trim().slice(0, maxLen);
  return /^[=+\-@]/.test(s) ? "'" + s : s;
}

/** 순위 조회: GET ?limit=10[&id=기록id]   기록 추가: GET ?submit=<기록 JSON> */
function doGet(e) {
  try {
    var p = (e && e.parameter) || {};
    if (p.submit) return add_(e, JSON.parse(p.submit));
    return reply_(limit_(e), p.id || '');
  } catch (err) {
    return fail_(err);
  }
}

/** 기록 추가: POST 본문 = 기록 JSON */
function doPost(e) {
  try {
    return add_(e, JSON.parse(e.postData.contents));
  } catch (err) {
    return fail_(err);
  }
}

/** 기록 한 줄 추가 후 순위를 돌려준다 */
function add_(e, d) {
  var lock = LockService.getScriptLock();
  try {
    lock.waitLock(10000);
    var id = text_(d.id, 32);
    if (!id) return fail_('id 없음');
    // 같은 기록이 두 번 오면(재전송) 한 번만 넣는다
    var ids = sheet_().getRange(1, 1, Math.max(1, sheet_().getLastRow()), 1).getValues();
    var exists = ids.some(function (r) { return String(r[0]) === id; });
    if (!exists) {
      sheet_().appendRow([
        id,
        text_(d.name, 12) || '무명 통신병',
        Math.max(0, Math.floor(Number(d.day) || 0)),
        Math.max(0, Math.floor(Number(d.score) || 0)),
        Math.max(0, Math.min(1, Number(d.accuracy) || 0)),
        d.hard === true,
        text_(d.rank, 16),
        text_(d.date, 20),
        new Date()
      ]);
    }
    return reply_(limit_(e), id);
  } catch (err) {
    return fail_(err);
  } finally {
    lock.releaseLock();
  }
}
