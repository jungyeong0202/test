#!/bin/sh
# 받은 GIF 에서 열세 마리를 모두 다시 들여온다.
#
# 포켓몬마다 쓴 명령을 여기 모아 둔다. 들여오기 도구를 고칠 때마다 손으로
# 열세 번 치지 않아도 되고, 어떤 값으로 만들었는지도 한눈에 보인다.
#
#     sh tools/reimport_all.sh
#     python3 tools/gen_sprites_cs.py
set -e
cd "$(dirname "$0")/.."

# 같은 그림이 이어지는 장은 하나로 합쳐 넘긴다.
frames() {
    python3 -c "
from PIL import Image
import sys; sys.path.insert(0,'tools')
import import_sprite as imp
path='assets/images/$1.gif'
with Image.open(path) as im: n=im.n_frames
seen=[]; last=None
for k in range(n):
    b=imp.read_rgba(path,k,quiet=True).tobytes()
    if b!=last: seen.append(k); last=b
print(','.join(map(str,seen)))"
}

go() {
    file=$1; shift
    echo "── $file"
    python3 tools/import_sprite.py "assets/images/$file.gif" \
        --native --facing left --idle-frames "$(frames "$file")" "$@" \
        | grep -E "잘라낸|한 바퀴" || true
}

go pikachu-f --key pikachu --name 피카츄 --colors 14 \
    --part lfoot:5,39,16,45 --part rfoot:18,39,28,45 \
    --motion "lfoot:0,0;0,-2;0,0;0,0" --motion "rfoot:0,0;0,0;0,0;0,-2" --pose-squash 1
go raichu-f --key raichu --name 라이츄 --colors 14 \
    --part lfoot:25,58,42,71 --part rfoot:43,58,56,71 \
    --motion "lfoot:0,0;0,-2;0,0;0,0" --motion "rfoot:0,0;0,0;0,0;0,-2" --pose-squash 1
go charmander --key charmander --name 파이리 --colors 16 \
    --part lfoot:7,34,18,41 --part rfoot:20,34,31,41 \
    --motion "lfoot:0,0;0,-2;0,0;0,0" --motion "rfoot:0,0;0,0;0,0;0,-2" --pose-squash 1
go charmeleon --key charmeleon --name 리자드 --colors 16 \
    --part lfoot:3,46,19,55 --part rfoot:20,46,33,55 \
    --motion "lfoot:0,0;0,-2;0,0;0,0" --motion "rfoot:0,0;0,0;0,0;0,-2" --pose-squash 1
go charizard --key charizard --name 리자몽 --colors 16 \
    --part lfoot:16,79,29,90 --part rfoot:34,79,47,90 \
    --motion "lfoot:0,0;0,-2;0,0;0,0" --motion "rfoot:0,0;0,0;0,0;0,-2" --pose-squash 1
go bulbasaur --key bulbasaur --name 이상해씨 --colors 16 \
    --part lfoot:4,28,12,37 --part rfoot:12,28,20,37 --part bfoot:23,28,31,37 \
    --motion "lfoot:0,0;0,-2;0,0;0,0" --motion "rfoot:0,0;0,0;0,0;0,-2" \
    --motion "bfoot:0,0;0,-1;0,0;0,0" --pose-squash 1
go ivysaur --key ivysaur --name 이상해풀 --colors 16 \
    --part lfoot:3,44,16,50 --part rfoot:17,44,29,50 --part bfoot:30,44,52,50 \
    --motion "lfoot:0,0;0,-2;0,0;0,0" --motion "rfoot:0,0;0,0;0,0;0,-2" \
    --motion "bfoot:0,0;0,-1;0,0;0,0" --pose-squash 1
go venusaur-f --key venusaur --name 이상해꽃 --colors 16 \
    --part lfoot:2,57,19,68 --part rfoot:20,57,46,68 --part bfoot:54,57,71,68 \
    --motion "lfoot:0,0;0,-2;0,0;0,0" --motion "rfoot:0,0;0,0;0,0;0,-2" \
    --motion "bfoot:0,0;0,-1;0,0;0,0" --pose-squash 1
go squirtle --key squirtle --name 꼬부기 --colors 16 \
    --part lfoot:3,37,15,42 --part rfoot:17,37,27,42 \
    --motion "lfoot:0,0;0,-2;0,0;0,0" --motion "rfoot:0,0;0,0;0,0;0,-2" --pose-squash 1
go wartortle --key wartortle --name 어니부기 --colors 16 \
    --part lfoot:9,45,22,56 --part rfoot:28,44,43,56 \
    --motion "lfoot:0,0;0,-2;0,0;0,0" --motion "rfoot:0,0;0,0;0,0;0,-2" --pose-squash 1
go blastoise --key blastoise --name 거북왕 --colors 16 \
    --part lfoot:11,53,31,64 --part rfoot:39,53,59,64 \
    --motion "lfoot:0,0;0,-2;0,0;0,0" --motion "rfoot:0,0;0,0;0,0;0,-2" --pose-squash 1
go ditto --key ditto --name 메타몽 --colors 12 --hop 3
go mew --key mew --name 뮤 --colors 16 --float \
    --part lleg:17,40,27,51 --part rleg:27,40,39,51 \
    --motion "lleg:0,0;0,-1;0,-2;0,-1" --motion "rleg:0,-2;0,-1;0,0;0,-1" --pose-squash 2

echo
echo "이제 python3 tools/gen_sprites_cs.py 를 돌리세요."
