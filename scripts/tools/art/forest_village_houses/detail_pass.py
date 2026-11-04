"""Exterior detail revision 3, installed by build_houses.py.

Keep footprints, anchors and structural silhouettes. Spend geometry on visible
construction: dressed corners, overlapping curved tiles, masonry, joinery and props.
"""
_base_wall = wall
_base_opening = opening
_base_create = Mesh.create
_base_tube = Mesh.tube
_base_hip_roof = hip_roof
_base_gable_roof = gable_roof
_base_room = room


def prism(m, a, b, width, depth, label, shade=None, bevel=.012):
    a,b=Vector(a),Vector(b)
    if (b-a).length < .00001: return
    axis=(b-a).normalized()
    helper=Vector((0,0,1)) if abs(axis.z)<.95 else Vector((0,1,0))
    u=axis.cross(helper).normalized()
    v=axis.cross(u).normalized()
    x,y=width*.5,depth*.5
    k=min(bevel,x*.30,y*.30)
    section=[(-x+k,-y),(x-k,-y),(x,-y+k),(x,y-k),(x-k,y),(-x+k,y),(-x,y-k),(-x,-y+k)]
    # A subtle bowed centre keeps long exposed joinery from looking machine cut.
    middle=(a+b)*.5+v*min(.009,(b-a).length*.004)
    centers=[a,middle,b] if label in ('Wood','WoodLight') and (b-a).length>.75 else [a,b]
    rings=[[p+u*xx+v*yy for xx,yy in section] for p in centers]
    col=tone(label,shade,variation=.018)
    for lo,hi in zip(rings,rings[1:]):
        for j in range(8):
            q=(j+1)%8
            m.poly([lo[j],lo[q],hi[q],hi[j]],label,col)
    m.poly(list(reversed(rings[0])),label,col)
    m.poly(rings[-1],label,col)


def soft_box(self, center, size, label='Wood', shade=None):
    c,s=Vector(center),Vector(size)
    axis=max(range(3),key=lambda i:s[i])
    direction=Vector((int(axis==0),int(axis==1),int(axis==2)))
    other=[s[i] for i in range(3) if i!=axis]
    prism(self,c-direction*s[axis]*.5,c+direction*s[axis]*.5,other[0],other[1],label,shade,
          .014 if label=='Stone' else .008)


def soft_beam(self,a,b,width=.10,depth=None,label='Wood',shade=None):
    prism(self,a,b,width,depth or width,label,shade,.010)


def round_tube(self,a,b,r=.10,label='Wood',shade=None,sides=8):
    first=len(self.faces)
    sides=max(sides,16 if r>.065 else 12)
    _base_tube(self,a,b,r,label,shade,sides)
    if not hasattr(self,'smooth_faces'): self.smooth_faces=set()
    self.smooth_faces.update(range(first,first+sides))


def detail_create(self):
    # Remove unseen inner wall faces and enclosed props; no furnished interior.
    keep=[]
    protected=getattr(self,'protected_faces',set())
    for index,face in enumerate(self.faces):
        center=sum((Vector(self.vertices[v]) for v in face),Vector())/len(face)
        hidden=index not in protected and self.labels[index]!='Glass' and any(all(low[a]<center[a]<high[a] for a in range(3)) for low,high in getattr(self,'room_volumes',[]))
        if not hidden:keep.append(index)
    removed=len(self.faces)-len(keep)
    old_smooth=getattr(self,'smooth_faces',set())
    self.smooth_faces={j for j,i in enumerate(keep) if i in old_smooth}
    self.faces=[self.faces[i] for i in keep]
    self.labels=[self.labels[i] for i in keep]
    self.colors=[self.colors[i] for i in keep]
    obj=_base_create(self)
    high_tris=len(obj.data.loop_triangles)
    assert 'DECIMATE' in [e.identifier for e in bpy.ops.object.modifier_add.get_rna_type().properties['type'].enum_items]
    for selected in list(bpy.context.selected_objects):selected.select_set(False)
    obj.select_set(True);bpy.context.view_layer.objects.active=obj
    for attempt in range(2):
        obj.data.calc_loop_triangles()
        count=len(obj.data.loop_triangles)
        if count<=19500:break
        modifier=obj.modifiers.new('ExteriorBudget','DECIMATE')
        modifier.ratio=18800/count
        modifier.use_collapse_triangulate=True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.data.validate(verbose=False,clean_customdata=True)
    obj.data.update()
    obj.data.calc_loop_triangles()
    triangles=len(obj.data.loop_triangles)
    assert 10000<=triangles<=20000,(self.name,triangles)
    obj.select_set(False)
    obj['detail_revision']=3
    obj['exterior_only']=True
    SPECS[self.name]['detail_revision']=3
    SPECS[self.name].update({'triangles':triangles,'polygons':len(obj.data.polygons),'vertices':len(obj.data.vertices),'exterior_only':True,'authoring_triangles':high_tris,'removed_hidden_faces':removed,'entrance_audit':self.entrance_audit})
    SPECS[self.name]['detail_features']=['curved_overlapping_roofing','chamfered_joinery','coursed_masonry','recessed_glazing_and_hardware','organic_household_props','all_four_elevations']
    print('OPTIMIZED',self.name,triangles,'triangles; hidden faces removed:',removed,'; entrance:',self.entrance_audit)
    return obj


def exterior_room(m,center,width,depth,height,label='Plaster',shade=None,holes=None,base=.16,boards=False,timber=True,collision=True):
    if not hasattr(m,'room_volumes'):m.room_volumes=[]
    x,y=center
    m.room_volumes.append(((x-width/2+.022,y-depth/2+.022,base+.02),(x+width/2-.022,y+depth/2-.022,base+height-.02)))
    _base_room(m,center,width,depth,height,label,shade,holes,base,boards,timber,collision)


def tile_shell(m, points, label, color, curvature=.018):
    # Four crosswise strips follow the bowed tile surface. All perimeter edges close.
    a,b,c,d=map(Vector,points) # lower left/right, upper right/left
    n=(b-a).cross(d-a).normalized()
    if n.z<0: n=-n
    lo=[]; hi=[]
    for j in range(5):
        t=j/4
        bulge=math.sin(math.pi*t)*curvature
        lo.append(a.lerp(b,t)+n*bulge)
        hi.append(d.lerp(c,t)+n*(bulge*.75+.008))
    for j in range(4):
        m.poly([lo[j],lo[j+1],hi[j+1],hi[j]],label,color)
        m.poly([lo[j]-n*.025,lo[j+1]-n*.025,lo[j+1],lo[j]],label,color)
        m.poly([hi[j],hi[j+1],hi[j+1]-n*.025,hi[j]-n*.025],label,color)
    m.poly([lo[0],hi[0],hi[0]-n*.025,lo[0]-n*.025],label,color)
    m.poly([lo[-1],lo[-1]-n*.025,hi[-1]-n*.025,hi[-1]],label,color)
    m.poly([a-n*.025,d-n*.025,c-n*.025,b-n*.025],label,color)


def detailed_roof(self,a,b,c,d,label='Roof',shade=None,course=.32,tile=.5):
    a,b,c,d=map(Vector,(a,b,c,d))
    self.slab([a,b,d,c],.10,label,shade)
    n=(b-a).cross(c-a).normalized()
    if n.z<0:n=-n
    slope=((c-a).length+(d-b).length)*.5
    if label=='Turf':
        # A continuous soil mat with overlapping, uneven sod plates and low leaves.
        self.slab([a+n*.015,b+n*.015,d+n*.015,c+n*.015],.10,'Wood','655b40')
        rows=max(2,math.ceil(slope/.34))
        for row in range(rows):
            t0,t1=row/rows,min(1,(row+1.16)/rows)
            l0,r0=a.lerp(c,t0),b.lerp(d,t0)
            l1,r1=a.lerp(c,t1),b.lerp(d,t1)
            cols=max(1,math.ceil((r0-l0).length/.38))
            for j in range(cols):
                p=[l0.lerp(r0,j/cols),l0.lerp(r0,(j+1)/cols),l1.lerp(r1,(j+1)/cols),l1.lerp(r1,j/cols)]
                self.slab([v+n*RNG.uniform(.065,.084) for v in p],.055,'Turf',tone('Turf','788b4c',.04))
        return
    straw=label=='Thatch'
    timber=label in ('Wood','WoodLight')
    slate=label=='Roof' and shade is not None and shade not in ('6c8050','778650')
    if straw:
        # Fine, tapered reed locks replace rectangular shingle-like straw blocks.
        cols=max(2,math.ceil((b-a).length/.058))
        for j in range(cols):
            t=(j+.5)/cols
            low=a.lerp(b,t);high=c.lerp(d,t)
            down=(low-high).normalized()
            across=(b-a).normalized()
            low+=down*RNG.uniform(.025,.095)+n*RNG.uniform(.015,.04)
            high+=n*RNG.uniform(.028,.05)
            w=(b-a).length/cols*.52
            color=tone('Thatch','c5a360',.06)
            mid=low.lerp(high,.50)+n*.023
            self.poly([low-across*w,low+down*.035,low+across*w,mid+across*w*.8,high+across*w*.72,high-across*w*.72,mid-across*w*.8],'Thatch',color)
            self.poly([low-across*w,mid-across*w*.8,high-across*w*.72,high-across*w*.72-n*.018,low-across*w-n*.018],'Thatch',color)
        return
    rows=max(2,math.ceil(slope/(.28 if not straw else .24)))
    for row in range(rows):
        t0,t1=row/rows,min(1,(row+1.12)/rows)
        l0,r0=a.lerp(c,t0),b.lerp(d,t0)
        l1,r1=a.lerp(c,t1),b.lerp(d,t1)
        tile_width=.10 if straw else (.20 if timber else .28)
        cols=max(1,math.ceil((r0-l0).length/tile_width))
        offset=.5 if row%2 else 0
        for j in range(-1,cols):
            s0=max(0,(j+offset)/cols);s1=min(1,(j+1+offset)/cols)
            if s1-s0<.0001:continue
            p=[l0.lerp(r0,s0),l0.lerp(r0,s1),l1.lerp(r1,s1),l1.lerp(r1,s0)]
            lift=n*(.020+RNG.uniform(0,.008))
            p=[v+lift for v in p]
            if (p[0]-p[1]).length<.012:continue
            col=tone(label,shade or ('b9724c' if label=='Roof' else None),.035)
            if slate or timber:
                # Staggered overlapping shingles have clipped corners and varied noses.
                p[0]+=n*RNG.uniform(0,.012);p[1]+=n*RNG.uniform(0,.012)
                self.slab(p,.022,label,col)
            else:
                tile_shell(self,p,label,col,.025 if not straw else .012)
    # Individual rafter tails, fascia step and soffit shadow beneath every eave.
    span=(b-a).length
    for j in range(max(2,math.ceil(span/.43))):
        t=(j+.5)/max(2,math.ceil(span/.43))
        p=a.lerp(b,t)-Vector((0,0,.13))
        q=c.lerp(d,t)-Vector((0,0,.13))
        toward=(q-p).normalized()
        self.beam(p-toward*.025,p+toward*.26,.065,.10,'Wood')
    self.beam(a-Vector((0,0,.075)),b-Vector((0,0,.075)),.07,.14,'WoodLight', '886343')


def ring(m,center,normal,radius=.06,thickness=.009,label='Iron',shade=None,segments=16):
    center,normal=Vector(center),Vector(normal).normalized()
    u=normal.cross(Vector((0,0,1)))
    if u.length<.001:u=normal.cross(Vector((1,0,0)))
    u.normalize();v=normal.cross(u)
    for j in range(segments):
        p=center+(u*math.cos(j*math.tau/segments)+v*math.sin(j*math.tau/segments))*radius
        q=center+(u*math.cos((j+1)*math.tau/segments)+v*math.sin((j+1)*math.tau/segments))*radius
        m.beam(p,q,thickness,thickness,label,shade)


def vessel(m,x,y,z=0,r=.18,h=.38,label='Roof',shade='a67550',barrel=False):
    # Turned pottery and coopered barrels: bulged profile, open mouth and inner lip.
    profile=[(0,.68),(.08,.88),(.35,1.0),(.72,.96),(.90,.73),(1,.70)] if not barrel else [(0,.82),(.15,.92),(.5,1),(.85,.92),(1,.82)]
    sides=16
    rows=[]
    for height,rad in profile:
        rows.append([Vector((x+math.cos(j*math.tau/sides)*r*rad,y+math.sin(j*math.tau/sides)*r*rad,z+h*height)) for j in range(sides)])
    if not hasattr(m,'smooth_faces'):m.smooth_faces=set()
    for a,b in zip(rows,rows[1:]):
        for j in range(sides):
            m.smooth_faces.add(len(m.faces))
            m.poly([a[j],a[(j+1)%sides],b[(j+1)%sides],b[j]],label,shade)
    top=rows[-1]
    inside=[Vector((x+(p.x-x)*.83,y+(p.y-y)*.83,p.z-.018)) for p in top]
    for j in range(sides):m.poly([top[j],top[(j+1)%sides],inside[(j+1)%sides],inside[j]],label,shade)
    m.poly([Vector((p.x,p.y,z+h*.87)) for p in reversed(inside)],'Wood','423c2f')
    m.poly(list(reversed(rows[0])),label,shade)
    if barrel:
        for zz,rad in ((.12,.91),(.5,1.015),(.87,.91)):
            ring(m,(x,y,z+h*zz),(0,0,1),r*rad,.021,'Iron',segments=16)
        for j in range(sides):
            ang=(j+.5)*math.tau/sides
            p=Vector((x+math.cos(ang)*r*.92,y+math.sin(ang)*r*.92,z+h*.15))
            q=Vector((p.x,p.y,z+h*.85))
            m.beam(p,q,.007,.009,'Wood','624c36')


def foliage(m,x,y,z,scale=.35,flowers=False):
    for j in range(7):
        ang=j*2.399
        direction=Vector((math.cos(ang),math.sin(ang),0))
        base=Vector((x,y,z))
        top=base+direction*scale*.60+Vector((0,0,scale*RNG.uniform(.8,1.3)))
        m.beam(base,top,.009,.010,'Plant','5b7044')
        for t in (.38,.7):
            p=base.lerp(top,t)
            for sign in (-1,1):
                side=Vector((-direction.y,direction.x,.25))*sign*scale*.23
                tip=p+side+direction*scale*.16+Vector((0,0,scale*.12))
                wing=direction*scale*.09
                m.poly([p,p+side*.5-wing,tip,p+side*.5+wing],'Plant', '6f8650')
        if flowers:
            for k in range(5):
                a=k*math.tau/5
                d=Vector((math.cos(a),math.sin(a),.12))*scale*.12
                cross=Vector((-d.y,d.x,0))*.6
                m.poly([top,top+d-cross,top+d*1.5,top+d+cross],'Plaster','d6bc74')


def lantern(m,origin,tangent,normal,u,z):
    p=plane_point(origin,tangent,normal,u,.22,z)
    facade_box(m,origin,tangent,normal,u,.05,z+.15,(.065,.12,.30),'Iron')
    m.beam(p+Vector(normal)*-.13+Vector((0,0,.27)),p+Vector((0,0,.27)),.035,.035,'Iron')
    m.box(p,(.12,.12,.21),'Glass','a28c58')
    for dx in (-.065,.065):
        for dy in (-.065,.065):m.beam(p+Vector((dx,dy,-.12)),p+Vector((dx,dy,.12)),.014,.015,'Iron')
    m.box(p+Vector((0,0,-.13)),(.17,.17,.035),'Iron')
    m.slab([p+Vector((-0.10,-.10,.13)),p+Vector((.10,-.10,.13)),p+Vector((.10,.10,.13)),p+Vector((-.10,.10,.13))],.04,'Iron')


def detailed_opening(m,origin,tangent,normal,u,z,width,height,door=False,shutters=True,shade=None,label=None,side='front'):
    first=len(m.faces)
    _base_opening(m,origin,tangent,normal,u,z,width,height,door,shutters,shade,label,side)
    P=lambda a,b,c:plane_point(origin,tangent,normal,a,b,c)
    if door:
        for sign in (-1,1):
            zz=z+sign*height*.28
            facade_box(m,origin,tangent,normal,u-width*.4,.040,zz,(.19,.025,.055),'Iron')
            m.tube(P(u-width*.5-.018,.075,zz-.06),P(u-width*.5-.018,.075,zz+.06),.018,'Iron')
            for du in (-.06,.04):m.tube(P(u-width*.4+du,.05,zz),P(u-width*.4+du,.066,zz),.008,'Iron')
        ring(m,P(u+width*.28,.104,z-.02),normal,.047,.010)
        facade_box(m,origin,tangent,normal,u,.002,z-height*.41,(width-.04,.06,.05),'WoodLight')
        if width>1.0:
            m.beam(P(u-width*.42,.04,z-height*.28),P(u+width*.42,.04,z+height*.28),.045,.055,'WoodLight')
        lantern(m,origin,tangent,normal,u+width*.5+.27,z+height*.35)
    else:
        cols=3 if width>.55 else 2
        for j in range(1,cols):
            facade_box(m,origin,tangent,normal,u-width/2+j*width/cols,-.005,z,(.022,.045,height-.04),'WoodLight')
        # Deeper stone/wood sill nose and a weather cap cast a clear window shadow.
        facade_box(m,origin,tangent,normal,u,.08,z+height*.5+.09,(width+.27,.30,.055),'Wood')
        facade_box(m,origin,tangent,normal,u,.14,z-height*.5-.14,(width+.33,.36,.05),'Stone')
        if shutters:
            for sign in (-1,1):
                xx=u+sign*(width*.5+.15)
                for j in range(3):
                    facade_box(m,origin,tangent,normal,xx-.075+j*.075,.085,z,(.064,.022,height-.03),'Wood',shade)
                m.beam(P(xx-.08,.105,z-height*.26),P(xx+.08,.105,z+height*.26),.023,.032,'WoodLight')
                for dz in (-height*.29,height*.29):
                    facade_box(m,origin,tangent,normal,xx,.12,z+dz,(.19,.018,.028),'Iron')
    for sign in (-1,1):
        for dz in (-height*.5,height*.5):
            m.tube(P(u+sign*(width*.5+.045),.13,z+dz),P(u+sign*(width*.5+.045),.142,z+dz),.010,'WoodLight','654d39')
    if not hasattr(m,'protected_faces'):m.protected_faces=set()
    m.protected_faces.update(range(first,len(m.faces)))
    if door:
        if not hasattr(m,'doorways'):m.doorways=[]
        m.doorways.append({'position':P(u,0,z-height/2),'normal':Vector(normal),'width':width,'height':height,'side':side})


def masonry_course(m,origin,tangent,normal,length,low,high,holes,full=False):
    row_height=.23 if full else .16
    rows=max(1,math.ceil((high-low)/row_height))
    for row in range(rows):
        za=low+(high-low)*row/rows;zb=low+(high-low)*(row+1)/rows
        width=.42 if full else .46
        cols=max(2,math.ceil(length/width))
        for j in range(-1,cols):
            a=max(-length/2,-length/2+(j+(row%2)*.5)*length/cols)
            b=min(length/2,-length/2+(j+1+(row%2)*.5)*length/cols)
            if b-a<.04:continue
            spans=[(a,b,za,zb)]
            for h in holes:
                out=[];x0,x1=h[0]-h[2]/2-.012,h[0]+h[2]/2+.012;z0,z1=h[1]-h[3]/2-.012,h[1]+h[3]/2+.012
                for aa,bb,cc,dd in spans:
                    if aa>=x1 or bb<=x0 or cc>=z1 or dd<=z0:out.append((aa,bb,cc,dd));continue
                    if aa<x0:out.append((aa,min(bb,x0),cc,dd))
                    if bb>x1:out.append((max(aa,x1),bb,cc,dd))
                    if cc<z0:out.append((max(aa,x0),min(bb,x1),cc,min(dd,z0)))
                    if dd>z1:out.append((max(aa,x0),min(bb,x1),max(cc,z1),dd))
                spans=out
            for aa,bb,cc,dd in spans:
                if bb-aa<.035 or dd-cc<.025:continue
                irregular_stone(m,origin,tangent,normal,aa+.006,bb-.006,cc+.006,dd-.006)


def irregular_stone(m,origin,tangent,normal,a,b,low,high):
    w,h=b-a,high-low
    cut=min(w*.18,h*.27)
    # Alternating rough corner cuts and broad painted faces read as fieldstone.
    points=[(a+cut,low),(b-cut*.7,low+.004),(b,low+cut),(b-.004,high-cut*.7),(b-cut,high),(a+cut*.8,high-.003),(a,high-cut),(a+.003,low+cut*.7)]
    depth=RNG.uniform(.027,.049)
    outer=[plane_point(origin,tangent,normal,u,depth,z) for u,z in points]
    inner=[plane_point(origin,tangent,normal,u,-.013,z) for u,z in points]
    col=tone('Stone','b5ad93',.055)
    m.poly(outer,'Stone',col)
    for j in range(8):m.poly([inner[j],inner[(j+1)%8],outer[(j+1)%8],outer[j]],'Stone',col)


def detailed_wall(m,origin,tangent,normal,length,height,holes=(),label='Plaster',shade=None,boards=False,side='front',base=.16,timber=True):
    _base_wall(m,origin,tangent,normal,length,height,holes,label,shade or ('e5d2aa' if label=='Plaster' else ('858671' if label=='Stone' else None)),False,side,base,timber)
    if label=='Stone':
        masonry_course(m,origin,tangent,normal,length,base,base+height,holes,True)
    else:
        masonry_course(m,origin,tangent,normal,length+.035,max(.02,base-.16),base+.30 if base<.3 else base,holes)
    if boards and m.name in ('04_carpenter','07_gatherer'):
        rows=math.ceil(height/.17)
        for row in range(rows):
            za=base+row*height/rows;zb=base+(row+1)*height/rows
            spans=[(-length/2+.025,length/2-.025)]
            for h in holes:
                if zb>h[1]-h[3]/2 and za<h[1]+h[3]/2:
                    spans=[(a,min(b,h[0]-h[2]/2-.01)) for a,b in spans if a<h[0]-h[2]/2-.01]+[(max(a,h[0]+h[2]/2+.01),b) for a,b in spans if b>h[0]+h[2]/2+.01]
            for a,b in spans:
                if b-a>.02:facade_box(m,origin,tangent,normal,(a+b)/2,.02,(za+zb)/2,(b-a,.052,zb-za-.012),'Wood',tone('Wood',shade or 'a07a4d',.026))
    elif boards:
        count=math.ceil(length/.18)
        for j in range(count):
            u=-length/2+(j+.5)*length/count
            spans=[(base+.02,base+height-.065)]
            for h in holes:
                if abs(u-h[0])<h[2]/2+.075:
                    spans=[(a,min(b,h[1]-h[3]/2-.015)) for a,b in spans if a<h[1]-h[3]/2-.015]+[(max(a,h[1]+h[3]/2+.015),b) for a,b in spans if b>h[1]+h[3]/2+.015]
            for a,b in spans:
                if b-a<.025:continue
                facade_box(m,origin,tangent,normal,u,.016,(a+b)/2,(length/count-.012,.035,b-a),'Wood',tone('Wood',shade or '8c6847',.035))
    # Timber end pegs and individually fitted eave dentils.
    for u in (-length*.5+.075,length*.5-.075):
        for zz in (base+.22,base+height-.15):
            p=plane_point(origin,tangent,normal,u,.12,zz)
            m.tube(p,p+Vector(normal)*.015,.018,'WoodLight','9c784e')
    m.features[side].append('coursed_masonry' if label=='Stone' else ('individual_boards' if boards else 'stone_plinth_and_joinery'))


def detailed_chimney(m,x,y,z,height=.85,width=.45):
    m.box((x,y,z+height*.5),(width-.015,width-.015,height),'Stone','7e8076')
    for origin,tangent,normal in [((x,y-width/2,0),(1,0,0),(0,-1,0)),((x,y+width/2,0),(-1,0,0),(0,1,0)),((x-width/2,y,0),(0,-1,0),(-1,0,0)),((x+width/2,y,0),(0,1,0),(1,0,0))]:
        masonry_course(m,origin,tangent,normal,width,z,z+height,(),True)
    for dx in (-1,1):m.box((x+dx*(width*.5+.025),y,z+height+.035),(.115,width+.21,.13),'Stone')
    for dy in (-1,1):m.box((x,y+dy*(width*.5+.025),z+height+.035),(width-.02,.115,.13),'Stone')
    m.box((x,y,z+height-.07),(width-.15,width-.15,.022),'Iron','323932')


def detailed_planter(m,x,y,z=.4,length=.70):
    for sign in (-1,1):
        m.box((x,y+sign*.13,z),(length,.045,.20),'WoodLight')
        m.box((x+sign*(length/2-.02),y,z),(.05,.28,.20),'WoodLight')
    m.box((x,y,z-.08),(length,.28,.04),'Wood')
    m.box((x,y,z+.055),(length-.08,.21,.035),'Wood','51462f')
    for j in range(max(2,round(length/.22))):foliage(m,x-length*.4+j*length*.8/max(1,round(length/.22)-1),y,z+.07,.20,True)


def detailed_logs(m,x,y,count=4,length=1.3):
    for j in range(count):
        xx=x+(j%3)*.22;zz=.12+(j//3)*.19
        m.tube((xx,y-length/2,zz),(xx,y+length/2,zz),.105,'Wood','6e5339',sides=16)
        for yy,normal in ((y-length/2-.008,(0,-1,0)),(y+length/2+.008,(0,1,0))):
            m.tube((xx,yy,zz),(xx,yy+normal[1]*.004,zz),.088,'WoodLight','b28c59',sides=16)
            for rad in (.030,.061):ring(m,(xx,yy+normal[1]*.006,zz),normal,rad,.004,'Wood','8b6941',segments=12)


def cap_course(m,a,b,label='Roof',shade=None,r=.105):
    a,b=Vector(a),Vector(b)
    axis=(b-a).normalized()
    cross=axis.cross(Vector((0,0,1)))
    if cross.length<.001:return
    cross.normalize();up=cross.cross(axis).normalized()
    count=max(1,math.ceil((b-a).length/.29))
    for j in range(count):
        p=a.lerp(b,j/count)+up*.025
        q=a.lerp(b,min(1,(j+1.10)/count))+up*.025
        color=tone(label,shade or 'b8754e',.024)
        rings=[]
        for pt in (p,q):
            rings.append([pt+cross*(math.cos(k*math.pi/8)*r)+up*(math.sin(k*math.pi/8)*r) for k in range(9)])
        for k in range(8):
            m.poly([rings[0][k],rings[0][k+1],rings[1][k+1],rings[1][k]],label,color)
            m.poly([rings[0][k]-up*.024,rings[1][k]-up*.024,rings[1][k+1]-up*.024,rings[0][k+1]-up*.024],label,color)
        for ring_ in rings:
            for k in range(8):m.poly([ring_[k],ring_[k+1],ring_[k+1]-up*.024,ring_[k]-up*.024],label,color)


def detailed_hip_roof(m,center,width,depth,eave,rise,label='Roof',shade=None):
    _base_hip_roof(m,center,width,depth,eave,rise,label,shade)
    x,y=center;cut=min(depth*.43,width*.32)
    a=(x-width/2+cut,y,eave+rise);b=(x+width/2-cut,y,eave+rise)
    cap_course(m,a,b,label,shade)
    for xx,peak in ((x-width/2,a),(x+width/2,b)):
        for yy in (y-depth/2,y+depth/2):cap_course(m,(xx,yy,eave),peak,label,shade)


def detailed_gable_roof(m,center,width,depth,eave,rise,label='Roof',shade=None,clip=0,cap=True):
    _base_gable_roof(m,center,width,depth,eave,rise,label,shade,clip,cap)
    if label=='Roof':
        x,y=center
        cap_course(m,(x,y-depth/2+clip,eave+rise),(x,y+depth/2-clip,eave+rise),label,shade)


def detailed_crate(m,x,y,z=0,size=.42):
    for j in range(4):
        offset=(j-1.5)*size/4
        m.box((x+offset,y,z+size-.015),(size/4-.008,size,.045),'WoodLight')
        for sign in (-1,1):
            m.box((x+offset,y+sign*size*.5,z+size*.5),(size/4-.008,.035,size),'Wood')
            m.box((x+sign*size*.5,y+offset,z+size*.5),(.035,size/4-.008,size),'Wood')
    for yy in (-size*.52,size*.52):
        for zz in (.10*size,.80*size):m.box((x,y+yy,z+zz),(size+.04,.035,.055),'WoodLight')
    for sign in (-1,1):
        m.beam((x-size*.40,y+sign*size*.53,z+.08*size),(x+size*.40,y+sign*size*.53,z+.9*size),.035,.04,'WoodLight')


def detail_props(m):
    name=m.name
    fit_entrance(m)
    ivy_sites={
        '01_chief':((-2.58,-.865,.16),(1,0,0),(0,-1,0),1.55),
        '02_host':((-1.32,.95,.16),(0,-1,0),(-1,0,0),1.44),
        '03_smith':((-2.38,1.67,.16),(-1,0,0),(0,1,0),1.50),
        '04_carpenter':((-2.70,-1.22,.16),(1,0,0),(0,-1,0),1.60),
        '05_herbalist':((.61,-1.49,.16),(1,0,0),(0,-1,0),1.60),
        '06_forester':((1.06,1.91,.20),(-1,0,0),(0,1,0),1.05),
        '07_gatherer':((-1.56,1.62,.75),(-1,0,0),(0,1,0),1.48),
        '08_farmer_east':((2.44,2.07,.16),(-1,0,0),(0,1,0),1.50),
        '09_farmer_west':((2.39,-1.57,.16),(1,0,0),(0,-1,0),1.62),
        '10_smallholder':((-2.05,-1.17,.16),(1,0,0),(0,-1,0),1.54)}
    origin,tangent,normal,height=ivy_sites[name]
    ivy(m,origin,tangent,normal,height)
    if name=='01_chief':
        for x in (-2.6,1.48):
            vessel(m,x,-1.27,.17,.18,.34);foliage(m,x,-1.27,.48,.36,True)
        # Public notice board and pegged papers on the back wall.
        m.box((-1.06,1.82,1.12),(.48,.10,.65),'Wood')
        for j in range(2):m.box((-1.17+j*.21,1.88,1.12),(.16,.016,.42),'Plaster','c9bd9a')
        vessel(m,2.6,2.2,.02,.23,.55,'Wood','92734b',True)
    elif name=='02_host':
        vessel(m,-1.64,.65,.02,.24,.58,'Wood','92734b',True)
        vessel(m,-1.64,1.25,.02,.17,.39,'Wood','8b6a45',True)
        for j in range(4):vessel(m,-.93+j*.24,-1.60,.69,.075,.16,'Roof','ad8154')
        for yy in (-.52,-.27,0,.27,.52):
            m.beam((2.32,yy,3.34),(2.32,yy,3.92),.032,.04,'WoodLight')
        m.features['back'].append('coopered_storage_barrels')
    elif name=='03_smith':
        # Forged anvil horn and a bellows pair by the repair hearth.
        m.slab([(2.24,-.31,.70),(2.88,-.31,.70),(3.12,-.15,.72),(2.88,-.02,.70),(2.24,-.02,.70)],.09,'Iron')
        for j in range(4):m.box((2.40,1.67,.62+j*.07),(.42,.27,.045),'Wood','8c6848')
        ring(m,(1.49,-.20,.86),(0,-1,0),.12,.018)
        vessel(m,-2.78,.95,.01,.18,.34,'Iron','686c61')
        m.features['right'].append('forged_horn_bellows_and_quench_tub')
    elif name=='04_carpenter':
        for j in range(4):
            m.beam((1.9+j*.14,1.2,.18),(1.73+j*.14,1.50,1.35+j*.09),.11,.045,'WoodLight')
        m.box((.98,-.48,.79),(.75,.16,.06),'Wood','9c774e')
        for xx in (.42,1.30):
            m.beam((xx,-.75,.48),(xx,-.75,.81),.022,.025,'Iron')
            m.beam((xx,-.75,.81),(xx,-.55,.81),.022,.025,'Iron')
        for j in range(5):m.box((-.60+j*.19,1.68,1.15),(.025,.045,.44),'Iron')
        m.features['back'].append('tool_rail_and_leaning_boards')
    elif name=='05_herbalist':
        for x,y,r,h in [(-.75,-1.70,.16,.32),(.75,-1.77,.19,.4),(3.0,1.0,.16,.37)]:
            vessel(m,x,y,.02,r,h);foliage(m,x,y,h,.32,True)
        for j in range(3):foliage(m,1.60+j*.28,-.79,1.12,.22)
        vessel(m,.1,1.82,.02,.25,.50,'Wood','8c724e',True)
        m.features['back'].append('herb_pottery_and_water_cask')
    elif name=='06_forester':
        detailed_logs(m,.95,-2.16,3,.42)
        # Dressed log-end pins and steel pull on the rear shutter.
        ring(m,(0,1.95,1.05),(0,1,0),.055,.010)
        for sign in (-1,1):
            m.beam((sign*.65,-1.94,1.78),(sign*.92,-1.94,1.46),.06,.07,'WoodLight')
        m.features['back'].append('cut_log_growth_rings_and_hardware')
    elif name=='07_gatherer':
        vessel(m,1.87,1.95,.76,.15,.35,'WoodLight','a78b5b')
        vessel(m,2.17,.90,.76,.16,.4,'WoodLight','998054')
        # Repeated binding collars wrap support posts; front stair route stays clear.
        for x in (-1.9,2.5):
            for zz in (.82,.85,.88):ring(m,(x,-2.12,zz),(0,0,1),.081,.012,'WoodLight','b19663',segments=12)
        for j in range(3):foliage(m,.92+j*.2,-1.6,1.69,.16)
        m.features['back'].append('woven_storage_and_post_bindings')
    elif name=='08_farmer_east':
        for x,y in [(-2.90,.90),(2.90,.8)]:
            vessel(m,x,y,.02,.16,.36);foliage(m,x,y,.35,.28,True)
        vessel(m,1.85,2.35,.02,.21,.50,'Wood','8f734c',True)
        m.features['back'].append('water_barrel_and_ceramic_planters')
    elif name=='09_farmer_west':
        for x,y in [(-2.37,1.88),(2.27,1.88)]:vessel(m,x,y,.02,.21,.51,'Wood','9f7c48',True)
        # Roof bundles are bound with curved horizontal willow stitches.
        for x in (-2.6,-1.8,-1.0,-.2,.6,1.4,2.2):
            for sign in (-1,1):
                m.beam((x,sign*1.95,2.04),(x,sign*1.56,2.62),.018,.022,'Wood','8f7748')
        m.features['back'].append('storage_casks_and_roof_lashings')
    elif name=='10_smallholder':
        vessel(m,-2.1,2.22,.02,.18,.42,'Wood','927450',True)
        for x,y in [(-2.40,.60),(2.76,1.65)]:
            vessel(m,x,y,.02,.13,.27);foliage(m,x,y,.25,.24)
        m.features['back'].append('household_pottery_and_cask')


def ivy(m,origin,tangent,normal,height):
    origin,tangent,normal=Vector(origin),Vector(tangent),Vector(normal)
    previous=origin+normal*.055
    for j in range(14):
        top=origin+Vector((0,0,height*(j+1)/14))+tangent*(math.sin(j*.8)*.07)+normal*.055
        m.beam(previous,top,.012,.014,'Plant','5c6d3c')
        for sign in (-1,1):
            base=previous.lerp(top,.5)
            tip=base+tangent*(sign*RNG.uniform(.10,.21))+Vector((0,0,.075))+normal*.035
            mid=base.lerp(tip,.52)+normal*.018
            wing=Vector((0,0,RNG.uniform(.045,.075)))
            col=tone('Plant','7c8f50',.06)
            m.poly([base,mid-wing,tip,mid+normal*.018],'Plant',col)
            m.poly([base,mid+normal*.018,tip,mid+wing],'Plant',col)
        previous=top


def fit_entrance(m):
    primary=min(m.doorways,key=lambda d:(d['position']-Vector(m.door)).length)
    error=(primary['position']-Vector(m.door)).length
    assert error<.01,(m.name,'Door marker does not match actual doorway',error)
    x,y,z=m.door
    if m.name=='08_farmer_east':steps(m,x,y-.70,1.06,z,2)
    landing_added=False
    if getattr(m,'stairs',[]):
        stair=min(m.stairs,key=lambda s:abs(s['x']-x))
        gap=y-stair['inner_edge']
        if .02<gap<.65:
            run=gap+.04
            m.box((x,(y+stair['inner_edge'])/2,z*.5),(stair['width'],run,z),'Stone')
            m.collider((x,(y+stair['inner_edge'])/2,z*.5),(stair['width'],run,z))
            landing_added=True
    # A human-width passage must stay clear between outside approach and door.
    approach_y=m.approach[1]
    for post in getattr(m,'posts',[]):
        overlap=abs(post['x']-x)<primary['width']/2+post['half_width'] and min(approach_y,y)<post['y']<max(approach_y,y)
        assert not overlap,(m.name,'Post obstructs doorway',post)
    point=Vector(m.door)+primary['normal']*.12
    supports=[]
    for collider in m.collisions:
        if 'size' not in collider:
            points=[Vector((p[0],-p[2],p[1])) for p in collider['points']]
            low=[min(p[a] for p in points) for a in range(3)];high=[max(p[a] for p in points) for a in range(3)]
            if low[0]<=point.x<=high[0] and low[1]<=point.y<=high[1]:
                top=high[2]*(point.y-low[1])/max(.001,high[1]-low[1])
                if abs(top-z)<.035:supports.append(top)
            continue
        cx,cz,ny=collider['position'];sx,sz,sy=collider['size'];cy=-ny
        if abs(point.x-cx)<=sx/2+.005 and abs(point.y-cy)<=sy/2+.005:
            top=cz+sz/2
            if abs(top-z)<.035:supports.append(top)
    assert supports,(m.name,'Door has no connected landing at threshold height',tuple(point),z)
    m.entrance_audit={'marker_error_m':error,'posts_clear':True,'threshold_supported':True,'landing_added':landing_added,'primary_width_m':primary['width'],'primary_height_m':primary['height'],'stand_point':[point.x,point.z,-point.y]}


# Existing design functions resolve these helpers at build time.
Mesh.box=soft_box
Mesh.beam=soft_beam
Mesh.tube=round_tube
Mesh.roof_panel=detailed_roof
Mesh.create=detail_create
wall=detailed_wall
room=exterior_room
opening=detailed_opening
chimney=detailed_chimney
planter=detailed_planter
logs=detailed_logs
hip_roof=detailed_hip_roof
gable_roof=detailed_gable_roof
crate=detailed_crate
# Dense coursed masonry replaces the old isolated decorative rectangular patches.
stone_patches=lambda *args,**kwargs:None
