﻿[<AutoOpen>]
module HOL.ProofUtils

open System.IO

type 'a Tree = Tree of 'a * 'a Tree list

type 'a Path = 
    | Top 
    | NodePath of 'a * 'a Tree list * 'a Path * 'a Tree list

type 'a Location = Loc of 'a Tree * 'a Path

let mkTree v xs = Tree(v,xs)

let left (Loc(t, p)) = 
    match p with
    | Top -> failwith "left at top"
    | NodePath(v,l::left, up, right) -> Loc(l, NodePath(v,left, up, t::right))
    | NodePath(v,[], up, right) -> failwith "left of first"

let right (Loc(t, p)) = 
    match p with 
    | Top -> failwith "right at top"
    | NodePath(v,left, up, r::right) -> Loc(r, NodePath(v,t::left, up, right))
    | _ -> failwith "right of last"

let down (Loc(t, p)) = 
    match t with
    | Tree(_,[]) -> failwith "down with Tree"
    | Tree(v,t1::trees) -> Loc(t1, NodePath(v,[], p, trees))

let up (Loc(t,p)) = 
    match p with
    | Top -> failwith "up of top"
    | NodePath(v,left,up,right) -> 
        Loc(Tree(v, ((left |> List.rev)@[t]) @ right),up)

let rec root (Loc (t, p) as l) = 
    match p with 
    | Top -> t
    | _ -> root (up l) 

let zipper t = Loc(t, Top)

let change t (Loc(_, p)) = Loc(t, p)

let insert_right r (Loc(t, p)) = 
    match p with
    | Top -> failwith "insert at top"
    | NodePath(v,left, up, right) -> Loc(t, NodePath(v,left, up, r::right))

let insert_left l (Loc(t, p)) =
    match p with
    | Top -> failwith "insert at top"
    | NodePath(v,left, up, right) -> Loc(t, NodePath(v,l::left, up, right))

let insert_down t1 (Loc(t, p)) = 
    match t with
    | Tree (v,[]) -> Loc(t1, NodePath(v,[], p, []))
    | Tree(v,ss) -> failwith "non empty"

let delete (Loc(_, p)) =
    match p with 
    | Top -> failwith "delete at top"
    | NodePath(v,left, up, r::right) -> Loc(r, NodePath(v,left, up, right))
    | NodePath(v,l::left, up, []) -> Loc(l, NodePath(v,left, up, []))
    | NodePath(v,[], up, []) -> Loc(Tree (v,[]), up)

let rec findNodePath (x:'a) (zipper:'a Location) =
    let (Loc(Tree(value,NodePaths), path)) = zipper
    if value = x then Some zipper
    else
        match NodePaths with
        | [] -> 
            //printf " |> right "
            match path with
            | NodePath(v,left,up,[]) -> None
            | _ -> zipper |> right |> (findNodePath x)
        | _ ->
            //printf " |> down "
            let down = zipper |> down |> findNodePath x
            match down with
            | Some x -> Some x
            | None -> 
                //printfn " |> right "
                zipper |> right |> (findNodePath x)

(* Proof Tree Implementation *)

type Exp = 
    | NullExp
    | Th  of thm
    | Te of term
    | Goal of (term list) * term

type InfRule = 
    | NullFun
    | TmFn of (term -> thm)
    | ThmFn of (thm -> thm)
    | ThmThmFn of (thm -> thm -> thm)
    | TmThmFn of (term -> thm -> thm)

type Proof = (Exp * string * InfRule)
type goal = (term list) * term

let showProof = ref false

(* Latex printing *)

let substs = 
    [
        "\\", "\\lambda "
        "\\lambda /","\\vee"
        "/\\lambda ","\\wedge"
        "~", "\\neg"
        "'a","\\alpha"
        "'b", "\\beta"
        "!", "\\forall "
        "==>","\\Rightarrow"
        "<=>", "\\Leftrightarrow"
        "->", "\\rightarrow"
        "|-", "\\vdash"
        " ", "\\ "
        "true", "\\top"
        "false", "\\bot"
        "?", "\\exists "
        "@", "\\epsilon "
    ]

let replace (x:string) (y:string) (s:string) = 
    s.Replace(x,y)

let strTolatex (s:string) = 
    substs
    |> List.fold 
        (
            fun acc (x,y) -> acc |> replace x y
        ) s

let rec printExp e = 
    match e with
    | Th t -> t |> print_thm |> strTolatex
    | Te t -> t |> print_term |> strTolatex
    | Goal (xs,t) -> 
        let asl = 
            xs
            |> List.map (fun t1 -> (Te t1) |> printExp)
            |> List.fold (fun acc t1 -> if acc = "" then t1 else acc + "," + t1) ""
        asl + "\\ ?\\ " + (Te t |> printExp)
    | NullExp -> ""

let rec treeToLatex ntabs exp (tr : Proof Tree) = 
    let tabs = "\t" |> String.replicate ntabs
    match tr with
    | Tree((p,s,_),xs) -> 
        match xs with
        | [] -> 
            let expStr = if p = exp then "\color{red}{" + (p |> printExp)  + "}" else (p |> printExp) 
            expStr + if s = "" then "" else "\; \mathbf{ " + s + "}"
        | _ -> 
            let prec = 
                xs
                |> List.fold 
                    (
                        fun acc x -> 
                            (
                                if acc = "" then "" 
                                else acc + ("\n"+ tabs + "\qquad\n" + tabs)) + (x |> treeToLatex (ntabs+1) exp)) ""
            sprintf "\\dfrac\n%s{%s}\n%s{%s}\n%s\\textsf{ %s}" tabs prec tabs (if p = exp then "\color{red}{" + (p |> printExp)  + "}" else (p |> printExp) ) tabs s

let view (loc:Proof Location) =
    let (Loc(Tree((exp,_,_),_), _)) = loc
    let proof = loc |> root
    let proofStr = proof |> treeToLatex 1 exp
    let html = sprintf "<!DOCTYPE html><html><head><script type=\"text/javascript\" src=\"https://cdn.mathjax.org/mathjax/latest/MathJax.js\">MathJax.Hub.Config({ config: [\"TeX-AMS-MML_HTMLorMML.js\"], 	extensions: [\"[a11y]/accessibility-menu.js\"], menuSettings: {	collapsible: true,	autocollapse: true,	explorer: true } });</script></head><body>\\[ \\small{ 	%s } \\]</body></html>" proofStr
    let path = System.IO.Path.GetTempFileName() + ".html"
    let mutable file = File.CreateText(path)
    file.WriteLine(html)
    file.Flush()
    System.Diagnostics.Process.Start(@"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",path) |> ignore
    loc

let start_proof (xs,s) = 
    let asl = xs |> List.map (fun x -> x |> parse_term)
    let (tr:Proof Tree) = mkTree(Goal (asl,(s |> parse_term)), "",NullFun) []
    tr |> zipper (*|> fun x -> if !showProof then view x else x*)

let prove (loc:Proof Location) = 
    let (Loc(Tree((ex,label,just_fn),children), path)) = loc

    match ex with
    | Goal(asl,t) ->
        let child = (Loc (Tree ((Goal (asl,t), label,just_fn),children),path)) |> down
        
        match just_fn with
        | TmFn f -> 
            match child with
            | (Loc (Tree ((Te t, "", NullFun),[]),p)) -> 
                loc |> change (Tree ((Th (f t), label, just_fn),children))
                //|> fun x -> if !showProof then view x else x
            | _ -> failwith "child is not term"
        | ThmFn f -> 
            match child with
            | (Loc (Tree ((Th t, _, _),_),_)) -> 
                loc |> change (Tree ((Th (f t), label, just_fn),children))
                //|> fun x -> if !showProof then view x else x
            | _ -> failwith "child is not thm"
        | ThmThmFn f -> 
            let child2 = child |> right
            match child with
            | (Loc (Tree ((Th t, _, _),_),_)) -> 
                match child2 with
                | (Loc (Tree ((Th t2, _, _),_),_)) -> 
                    loc |> change (Tree ((Th (f t t2), label, just_fn),children))
                    //|> fun x -> if !showProof then view x else x
                | _ -> failwith "child2 is not thm"
            | _ -> failwith "child1 is not thm"
        | TmThmFn f -> 
            let child2 = child |> right
            match child with
            | (Loc (Tree ((Te t, _, _),_),_)) -> 
                match child2 with
                | (Loc (Tree ((Th t2, _, _),_),_)) -> 
                    loc |> change (Tree ((Th (f t t2), label, just_fn),children))
                    //|> fun x -> if !showProof then view x else x
                | _ -> failwith "child2 is not thm"
            | _ -> failwith "child1 is not tm"
        | NullFun -> failwith "no rule given"
        
    | _ -> failwith "not a goal"

let loc_thm (loc:Proof Location) = 
    match loc with
    | Loc(Tree((Th t,_,_),_), _) -> Some t
    | _ -> None

let loc_term (loc:Proof Location) = 
    match loc with
    | Loc(Tree((Te t,_,_),_), _) -> Some t
    | _ -> None

let lower : (Proof Location -> Proof Location) = up

let mkGoal xs t = 
    let asl = xs |> List.map (fun x -> x |> parse_term)
    let t = t |> parse_term
    Goal (asl,t)

let rec findExp x (loc:Proof Location) =
    let (Loc(Tree((exp,_,_),NodePaths), path)) = loc
    if exp = x then Some loc
    else
        match NodePaths with
        | [] -> 
            //printf "%s" (exp |> printExp)
            //printf " |> right "
            match path with
            | NodePath(v,left,up,[]) -> None
            | _ -> loc |> right |> (findExp x)
        | _ ->
            //printf "%s" (exp |> printExp)
            //printf " |> down "
            let down = loc |> down |> findExp x
            match down with
            | Some x -> Some x
            | None -> 
                //printf "%s" (exp |> printExp)
                //printfn " |> right "
                loc |> right |> (findExp x)

let select exp loc = (findExp exp loc) |> Option.get

let thm_fd lbl th = 
    mkTree(Th th, lbl, NullFun) []

let term_fd t = 
    mkTree(Te t, "", NullFun) []

let by thm lbl loc = change (thm |> thm_fd lbl) loc

let tmFnForward lbl jf t = 
    match jf with
    | TmFn f -> 
        let th = t |> f
        let (tr:Proof Tree) = mkTree (Th th,lbl,TmFn f) [mkTree (Te t,"",NullFun) []]
        tr
    | _ -> failwith "not TmFn"

let thmFnForward lbl jf t = 
    match jf with
    | ThmFn f -> 
        let th = t |> zipper |> loc_thm |> Option.get |> f
        let (tr:Proof Tree) = mkTree (Th th,lbl,ThmFn f) [t]
        tr
    | _ -> failwith "not ThmFn"

let thmThmFnForward lbl jf t1 t2 = 
    match jf with
    | ThmThmFn f -> 
        let th1 = t1 |> zipper |> loc_thm |> Option.get
        let th2 = t2 |> zipper |> loc_thm |> Option.get
        let th = f th1 th2
        let (tr:Proof Tree) = 
            mkTree 
                (Th th,lbl,ThmThmFn f) 
                [t1; t2]
        tr
    | _ -> failwith "not ThmFn"

let tmThmFnForward lbl jf t t2 = 
    match jf with
    | TmThmFn f -> 
        let th2 = t2 |> zipper |> loc_thm |> Option.get
        let th = f t th2
        let (tr:Proof Tree) = 
            mkTree 
                (Th th,lbl,TmThmFn f) 
                [t |> term_fd; t2]
        tr
    | _ -> failwith "not ThmFn"

(* Rules *)

let accept thm str loc = 
    let (Loc(Tree((ex,label,just_fn),children), path)) = loc
    match ex with
    | Goal(asl,t) ->
        let (asl1,t2) = dest_thm thm
        if asl1 = asl && t = t2 then
            loc
            |> change (Tree ((Th thm, str, NullFun),children))
        else failwith "don't match"
    | _ -> failwith "not a goal"

let refl_conv_fd t = tmFnForward "refl_conv" (TmFn refl_conv) t
//let refl_conv_bk = tmFnBackward "refl_conv" (TmFn refl_conv)

let refl_conv_bk = 
    fun (loc: Proof Location) -> 
        let (Loc(Tree((ex,_,_),children), _)) = loc
        match ex with
        | Goal(asl,t) ->
            let (ant,_) = dest_eq t
            loc
            |> change (Tree ((Goal (asl,t), "refl_conv", TmFn refl_conv),children))
            |> insert_down (mkTree((Te ant),"", NullFun) []) 
        | _ -> failwith "not a goal"

let sym_rule_fd = thmFnForward "sym_rule" (ThmFn sym_rule)

let sym_rule_bk = 
    fun (loc: Proof Location) -> 
        let (Loc(Tree((ex,_,_),children), _)) = loc
        match ex with
        | Goal(asl,t) ->
            let (t1,t2) = dest_eq t
            let g1 = Goal (asl,(mk_eq (t2,t1)))
            loc
            |> change (Tree ((Goal (asl,t), "sym_rule", ThmFn sym_rule),children))
            |> insert_down (mkTree(g1, "", NullFun) []) 
        | _ -> failwith "not a goal"

let eq_mp_rule_fd t1 t2 = thmThmFnForward "eq_mp_rule" (ThmThmFn eq_mp_rule) t1 t2

let eq_mp_rule_bk g2 = //thmThmFnBackward "eq_mp_rule" (ThmThmFn eq_mp_rule) g2
    fun (loc: Proof Location) -> 
        let (Loc(Tree((ex,_,_),children), _)) = loc
        match ex with
        | Goal(asl,t) ->
            match g2 with
            | Goal(asl2,t2) -> 
                let asl1 = 
                    asl |> List.filter (fun x -> not (asl2 |> List.contains x))
                let g1 = Goal (asl1,(mk_eq (t2,t)))
                loc
                |> change (Tree ((Goal (asl,t), "eq_mp_rule", ThmThmFn eq_mp_rule),children))
                |> insert_down (mkTree(g1, "", NullFun) []) 
                |> insert_right (mkTree(g2, "", NullFun) []) 
            | _-> failwith "the given is not a goal"
        | _ -> failwith "not a goal"

let assume_rule_fd t = tmFnForward "assume_rule" (TmFn assume_rule) t

let assume_rule_bk = 
    fun (loc: Proof Location) -> 
        let (Loc(Tree((ex,_,_),children), _)) = loc
        match ex with
        | Goal(xs,t) when xs = [t] ->
            loc
            |> change (Tree ((ex, "assume_rule", TmFn assume_rule),children))
            |> insert_down (mkTree((Te t),"", NullFun) []) 
            //|> fun x -> if !showProof then view x else x
        | _ -> failwith "can't apply rule"

let deduct_antisym_rule_fd t1 t2 = thmThmFnForward "deduct_antisym_rule" (ThmThmFn deduct_antisym_rule) t1 t2

let deduct_antisym_rule_bk asl1 asl2 loc = 
    let (Loc(Tree((ex,_,_),children), _)) = loc
    match ex with
    | Goal(asl,t) ->
        let (t1,t2) = dest_eq t
        loc
        |> change (Tree ((Goal (asl,t), "deduct_antisym_rule", ThmThmFn deduct_antisym_rule),children))
        |> insert_down (mkTree(Goal(asl1@[t2],t1), "", NullFun) []) 
        |> insert_right (mkTree(Goal(asl2@[t1],t2), "", NullFun) []) 
        //|> fun x -> if !showProof then view x else x
    | _ -> failwith "not a goal"

let contr_rule_fd = tmThmFnForward "contr_rule" (TmThmFn contr_rule)

let contr_rule_bk t1 loc = 
    let (Loc(Tree((ex,_,_),children), _)) = loc
    match ex with
    | Goal(asl,t) ->
        loc
        |> change (Tree ((Goal (asl,t), "contr_rule", TmThmFn contr_rule),children))
        |> insert_down (mkTree(Te t, "", NullFun) []) 
        |> insert_right (mkTree(Goal(asl,t1), "", NullFun) []) 
        //|> fun x -> if !showProof then view x else x
    | _ -> failwith "not a goal"

let eqf_intro_rule_fd = thmFnForward "eqf_intro_rule" (ThmFn eqf_intro_rule)

let eqf_intro_rule_bk loc = 
    let (Loc(Tree((ex,_,_),children), _)) = loc
    match ex with
    | Goal(asl,t) ->
        let (t1,t2) = dest_eq t
        loc
        |> change (Tree ((Goal (asl,t), "eqf_intro_rule", ThmFn eqf_intro_rule),children))
        |> insert_down (mkTree(Goal(asl,t1 |> mk_not), "", NullFun) []) 
    | _ -> failwith "not a goal"

let not_intro_rule_fd = thmFnForward "not_intro_rule" (ThmFn not_intro_rule)

let not_intro_rule_bk = 
    fun (loc: Proof Location) -> 
        let (Loc(Tree((ex,_,_),children), _)) = loc
        match ex with
        | Goal(asl,t)  ->
            let t1 = mk_imp (dest_not t,false_tm)
            loc
            |> change (Tree ((ex, "not_intro", ThmFn not_intro_rule),children))
            |> insert_down (mkTree(Goal (asl,t1),"", NullFun) [])   
            //|> fun x -> if !showProof then view x else x
        | _ -> failwith "can't apply rule"

let disch_rule_fd = tmThmFnForward "disch_rule" (TmThmFn disch_rule)

let disch_rule_bk = 
    fun (loc: Proof Location) -> 
        let (Loc(Tree((ex,_,_),children), _)) = loc
        match ex with
        | Goal(asl,t)  ->
            let (t1,t2) = dest_imp t
            let asl2 = asl |> List.filter (fun x -> x <> t1)
            loc
            |> change (Tree ((ex, "disch_rule", TmThmFn disch_rule),children))
            |> insert_down (mkTree((Te t1),"", NullFun) []) 
            |> insert_right (mkTree(Goal (asl2@[t1],t2),"", NullFun) []) 
            //|> fun x -> if !showProof then view x else x
        | _ -> failwith "can't apply rule"

let add_asm_rule_bk t1 loc = 
    let (Loc(Tree((ex,_,_),children), _)) = loc
    match ex with
    | Goal(asl,t) ->
        let asl1 = asl |> List.filter (fun x -> x <> t1)
        loc
        |> change (Tree ((Goal (asl,t), "add_asm_rule", TmThmFn add_asm_rule),children))
        |> insert_down (mkTree(Te t1, "", NullFun) []) 
        |> insert_right (mkTree(Goal(asl1,t), "", NullFun) []) 
        //|> fun x -> if !showProof then view x else x
    | _ -> failwith "not a goal"

let assume_rule_tr t = 
    let th = t |> assume_rule
    (th, mkTree (Th th,"assume_rule") [mkTree (Te t,"") []])

let eta_conv_tr t = 
    let th = t |> eta_conv
    (th, mkTree (Th th,"eta_conv") [mkTree (Te t,"") []])

let refl_conv_tr t = 
    let th = t |> refl_conv
    (th, mkTree (Th th,"refl_conv") [mkTree (Te t,"") []])

let sym_rule_tr (th1,g1) = 
    let th = th1 |> sym_rule
    (th, mkTree (Th th,"sym_rule") [g1])

let spec_rule_tr t (th1,g1) = 
    let th = th1 |> spec_rule t
    (th, mkTree (Th th,"spec_rule") [mkTree (Te t,"") [];g1])

let mk_abs_rule_tr t (th1,g1) = 
    let th = th1 |> mk_abs_rule t
    (th, mkTree (Th th,"mk_abs_rule") [mkTree (Te t,"") [];g1])

let list_trans_rule_tr xs = 
    let thms = xs |> List.map (fun (th,_) -> th)
    let gs = xs |> List.map (fun (_,g) -> g)
    let th = thms |> list_trans_rule 
    (th, mkTree (Th th,"list_trans_rule") gs)

let mk_comb1_rule_tr (th1,g1) t = 
    let th = mk_comb1_rule th1 t
    (th, mkTree (Th th,"mk_comb1_rule") [g1;mkTree (Te t,"") []])

let disj1_rule_tr (th1,g1) t = 
    let th = disj1_rule th1 t
    (th, mkTree (Th th,"disj1_rule") [g1;mkTree (Te t,"") []])

let gen_rule_tr t (th1,g1) = 
    let th = th1 |> gen_rule t
    (th, mkTree (Th th,"gen_rule") [mkTree (Te t,"") [];g1])

let deduct_antisym_rule_tr (th1,g1) (th2,g2) = 
    let th = deduct_antisym_rule th1 th2
    (th, mkTree (Th th,"deduct_antisym_rule") [g1;g2])

let list_gen_rule_tr xs (th1,g1) = 
    let trms = xs |> List.map (fun t -> mkTree (Te t,"") [])
    let th = list_gen_rule xs th1
    (th, mkTree (Th th,"list_gen_rule") (trms@[g1]))

let eq_mp_rule_tr (th1,g1) (th2,g2) = 
    let th = eq_mp_rule th1 th2
    (th, mkTree (Th th,"eq_mp_rule") [g1;g2])

let exists_rule_tr (t1,t2) (th1,g1) = 
    let th = th1 |> exists_rule (t1,t2)
    (th, mkTree (Th th,"exists_rule") [mkTree (Te t1,"") [];mkTree (Te t2,"") [];g1])

let select_rule_tr (th1,g1) = 
    let th = th1 |> select_rule
    (th, mkTree (Th th,"select_rule") [g1])

let mk_eq_rule_tr (th1,g1) (th2,g2) = 
    let th = mk_eq_rule th1 th2
    (th, mkTree (Th th,"mk_eq_rule") [g1;g2])

let disj2_rule_tr t (th1,g1) = 
    let th = th1 |> disj2_rule t
    (th, mkTree (Th th,"disj2_rule") [mkTree (Te t,"") [];g1])

let mk_select_rule_tr t (th1,g1) = 
    let th = th1 |> mk_select_rule t
    (th, mkTree (Th th,"mk_select_rule") [mkTree (Te t,"") [];g1])

let disch_rule_tr t (th1,g1) = 
    let th = th1 |> disch_rule t
    (th, mkTree (Th th,"disch_rule") [mkTree (Te t,"") [];g1])

let not_intro_rule_tr (th1,g1) = 
    let th = th1 |> not_intro_rule
    (th, mkTree (Th th,"not_intro_rule") [g1])

let disj_cases_rule_tr (th1,g1) (th2,g2) (th3,g3) = 
    let th = disj_cases_rule th1 th2 th3
    (th, mkTree (Th th,"disj_cases_rule") [g1;g2;g3])

let eqt_intro_rule_tr (th1,g1) = 
    let th = th1 |> eqt_intro_rule
    (th, mkTree (Th th,"eqt_intro_rule") [g1])

let eqf_intro_rule_tr (th1,g1) = 
    let th = th1 |> eqf_intro_rule
    (th, mkTree (Th th,"eqf_intro_rule") [g1])

let contr_rule_tr t (th1,g1) = 
    let th = th1 |> contr_rule t
    (th, mkTree (Th th,"contr_rule") [mkTree (Te t,"") [];g1])

let eqf_elim_rule_tr (th1,g1) = 
    let th = th1 |> eqf_elim_rule
    (th, mkTree (Th th,"eqf_elim_rule") [g1])

let undisch_rule_tr (th1,g1) = 
    let th = th1 |> undisch_rule
    (th, mkTree (Th th,"undisch_rule") [g1])

let conjunct1_rule_tr (th1,g1) = 
    let th = th1 |> conjunct1_rule
    (th, mkTree (Th th,"conjunct1_rule") [g1])

let conjunct2_rule_tr (th1,g1) = 
    let th = th1 |> conjunct2_rule
    (th, mkTree (Th th,"conjunct2_rule") [g1])

let conj_rule_tr (th1,g1) (th2,g2) = 
    let th = conj_rule th1 th2
    (th, mkTree (Th th,"conj_rule") [g1;g2])

let deduct_contrapos_rule_tr t (th1,g1) = 
    let th = th1 |> deduct_contrapos_rule t
    (th, mkTree (Th th,"deduct_contrapos_rule") [mkTree (Te t,"") [];g1])

let not_elim_rule_tr (th1,g1) = 
    let th = th1 |> not_elim_rule
    (th, mkTree (Th th,"not_elim_rule") [g1])